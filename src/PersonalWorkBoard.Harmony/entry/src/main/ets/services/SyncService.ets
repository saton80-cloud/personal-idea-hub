import { util } from '@kit.ArkTS';
import {
  PendingMutation,
  SessionState,
  SyncConflict,
  SyncEnvelope,
  SyncSummary,
  WorkItem,
  WorkTask
} from '../models/Contracts';
import { ApiClient } from './ApiClient';
import { StorageService } from './StorageService';

export class SyncService {
  private static readonly shared: SyncService = new SyncService();
  private readonly storage: StorageService = StorageService.getInstance();
  private readonly api: ApiClient = ApiClient.getInstance();

  static getInstance(): SyncService {
    return SyncService.shared;
  }

  async queueItem(item: WorkItem): Promise<void> {
    const items = await this.storage.getItems();
    const existingIndex = items.findIndex((candidate: WorkItem) => candidate.id === item.id);
    if (existingIndex >= 0) {
      items[existingIndex] = item;
    } else {
      items.unshift(item);
    }
    await this.storage.saveItems(items);
    await this.queueMutation('WorkItem', item.id, item.rowVersion, JSON.stringify(item));
  }

  async queueTask(task: WorkTask): Promise<void> {
    const tasks = await this.storage.getTasks();
    const existingIndex = tasks.findIndex((candidate: WorkTask) => candidate.id === task.id);
    if (existingIndex >= 0) {
      tasks[existingIndex] = task;
    } else {
      tasks.unshift(task);
    }
    await this.storage.saveTasks(tasks);
    await this.queueMutation('WorkTask', task.id, task.rowVersion, JSON.stringify(task));
  }

  private async queueMutation(entityType: string, entityId: string, baseRowVersion: number,
    payloadJson: string): Promise<void> {
    const pending = await this.storage.getPending();
    const replacementIndex = pending.findIndex((candidate: PendingMutation) =>
      candidate.entityType === entityType && candidate.entityId === entityId);
    const mutation: PendingMutation = {
      mutationId: util.generateRandomUUID(true),
      entityType: entityType,
      entityId: entityId,
      operation: 'Upsert',
      baseRowVersion: baseRowVersion,
      payloadJson: payloadJson,
      clientChangedAt: new Date().toISOString()
    };
    if (replacementIndex >= 0) {
      pending[replacementIndex] = mutation;
    } else {
      pending.push(mutation);
    }
    await this.storage.savePending(pending);
  }

  async synchronize(session: SessionState): Promise<SyncSummary> {
    let pushed = 0;
    let pulled = 0;
    let conflictCount = 0;
    let recentConflicts: SyncConflict[] = [];
    let pending = await this.storage.getPending();

    if (pending.length > 0) {
      const batch = pending.slice(0, 200);
      const pushResult = await this.api.push(session, batch);
      pushed = pushResult.acceptedMutationIds.length;
      conflictCount = pushResult.conflicts.length;
      recentConflicts = pushResult.conflicts;
      const finishedIds: string[] = pushResult.acceptedMutationIds.slice();
      pushResult.conflicts.forEach((conflict: SyncConflict) => finishedIds.push(conflict.mutationId));
      pending = pending.filter((mutation: PendingMutation) => !finishedIds.includes(mutation.mutationId));
      await this.storage.savePending(pending);
      if (pushResult.conflicts.length > 0) {
        const storedConflicts = await this.storage.getConflicts();
        await this.storage.saveConflicts(pushResult.conflicts.concat(storedConflicts).slice(0, 100));
      }
    }

    const currentSequence = await this.storage.getSequence();
    const pullResult = await this.api.pull(session, currentSequence);
    const remaining = await this.storage.getPending();
    const protectedKeys = new Set<string>();
    remaining.forEach((mutation: PendingMutation) =>
      protectedKeys.add(`${mutation.entityType}:${mutation.entityId}`));
    recentConflicts.forEach((conflict: SyncConflict) =>
      protectedKeys.add(`${conflict.entityType}:${conflict.entityId}`));

    let items = await this.storage.getItems();
    let tasks = await this.storage.getTasks();
    pullResult.changes.forEach((change: SyncEnvelope) => {
      if (protectedKeys.has(`${change.entityType}:${change.entityId}`)) {
        return;
      }
      if (change.entityType === 'WorkItem') {
        items = this.applyItemChange(items, change);
        pulled++;
      } else if (change.entityType === 'WorkTask') {
        tasks = this.applyTaskChange(tasks, change);
        pulled++;
      }
    });
    await this.storage.saveItems(items);
    await this.storage.saveTasks(tasks);
    await this.storage.saveConflicts(recentConflicts);
    await this.storage.saveSequence(pullResult.latestSequence);
    await this.storage.saveLastSyncAt(new Date().toLocaleString());
    return {
      pushed: pushed,
      pulled: pulled,
      conflicts: conflictCount,
      latestSequence: pullResult.latestSequence
    };
  }

  private applyItemChange(items: WorkItem[], change: SyncEnvelope): WorkItem[] {
    const filtered = items.filter((item: WorkItem) => item.id !== change.entityId);
    if (change.operation !== 'Delete') {
      filtered.unshift(JSON.parse(change.payloadJson) as WorkItem);
    }
    return filtered;
  }

  private applyTaskChange(tasks: WorkTask[], change: SyncEnvelope): WorkTask[] {
    const filtered = tasks.filter((task: WorkTask) => task.id !== change.entityId);
    if (change.operation !== 'Delete') {
      filtered.unshift(JSON.parse(change.payloadJson) as WorkTask);
    }
    return filtered;
  }
}
