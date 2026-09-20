import { common } from '@kit.AbilityKit';
import { preferences } from '@kit.ArkData';
import { PendingMutation, SessionState, SyncConflict, WorkItem, WorkTask } from '../models/Contracts';

const STORE_NAME: string = 'personal_work_board_harmony';

export class StorageService {
  private static readonly shared: StorageService = new StorageService();
  private store: preferences.Preferences | undefined = undefined;

  static getInstance(): StorageService {
    return StorageService.shared;
  }

  async initialize(context: common.Context): Promise<void> {
    if (this.store !== undefined) {
      return;
    }
    this.store = await preferences.getPreferences(context, { name: STORE_NAME });
  }

  private requireStore(): preferences.Preferences {
    if (this.store === undefined) {
      throw new Error('本地存储尚未初始化');
    }
    return this.store;
  }

  private async getText(key: string, fallback: string): Promise<string> {
    const value = await this.requireStore().get(key, fallback);
    return value as string;
  }

  private async putText(key: string, value: string): Promise<void> {
    const localStore = this.requireStore();
    await localStore.put(key, value);
    await localStore.flush();
  }

  async getSession(): Promise<SessionState | null> {
    const json = await this.getText('session', '');
    if (json.length === 0) {
      return null;
    }
    return JSON.parse(json) as SessionState;
  }

  async saveSession(session: SessionState): Promise<void> {
    await this.putText('session', JSON.stringify(session));
  }

  async clearSession(): Promise<void> {
    const localStore = this.requireStore();
    await localStore.delete('session');
    await localStore.flush();
  }

  async getItems(): Promise<WorkItem[]> {
    const json = await this.getText('work_items', '[]');
    return JSON.parse(json) as WorkItem[];
  }

  async saveItems(items: WorkItem[]): Promise<void> {
    await this.putText('work_items', JSON.stringify(items));
  }

  async getTasks(): Promise<WorkTask[]> {
    const json = await this.getText('work_tasks', '[]');
    return JSON.parse(json) as WorkTask[];
  }

  async saveTasks(tasks: WorkTask[]): Promise<void> {
    await this.putText('work_tasks', JSON.stringify(tasks));
  }

  async getPending(): Promise<PendingMutation[]> {
    const json = await this.getText('pending_mutations', '[]');
    return JSON.parse(json) as PendingMutation[];
  }

  async savePending(mutations: PendingMutation[]): Promise<void> {
    await this.putText('pending_mutations', JSON.stringify(mutations));
  }

  async getConflicts(): Promise<SyncConflict[]> {
    const json = await this.getText('sync_conflicts', '[]');
    return JSON.parse(json) as SyncConflict[];
  }

  async saveConflicts(conflicts: SyncConflict[]): Promise<void> {
    await this.putText('sync_conflicts', JSON.stringify(conflicts));
  }

  async getSequence(): Promise<number> {
    const text = await this.getText('last_sequence', '0');
    const value = Number(text);
    return Number.isNaN(value) ? 0 : value;
  }

  async saveSequence(sequence: number): Promise<void> {
    await this.putText('last_sequence', sequence.toString());
  }

  async getServerHint(): Promise<string> {
    return await this.getText('server_hint', 'http://192.168.1.100');
  }

  async saveServerHint(serverUrl: string): Promise<void> {
    await this.putText('server_hint', serverUrl);
  }

  async getLastSyncAt(): Promise<string> {
    return await this.getText('last_sync_at', '尚未同步');
  }

  async saveLastSyncAt(value: string): Promise<void> {
    await this.putText('last_sync_at', value);
  }

  async getMorningReminder(): Promise<string> {
    return await this.getText('morning_reminder', '08:30');
  }

  async saveMorningReminder(value: string): Promise<void> {
    await this.putText('morning_reminder', value);
  }

  async getEveningReminder(): Promise<string> {
    return await this.getText('evening_reminder', '18:00');
  }

  async saveEveningReminder(value: string): Promise<void> {
    await this.putText('evening_reminder', value);
  }
}
