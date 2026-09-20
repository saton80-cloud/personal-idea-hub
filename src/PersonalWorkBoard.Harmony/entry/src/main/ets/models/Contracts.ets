export interface LoginRequest {
  userName: string;
  password: string;
  deviceName: string;
  devicePlatform: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  userId: string;
  deviceId: string;
  displayName: string;
}

export interface PairingResponse {
  accessToken: string;
  expiresAt: string;
  userId: string;
  deviceId: string;
  serverName: string;
}

export interface WorkItem {
  id: string;
  parentId: string | null;
  type: string;
  status: string;
  priority: string;
  title: string;
  description: string;
  progress: number;
  nextAction: string;
  targetDate: string | null;
  currentVersion: string;
  rowVersion: number;
  createdAt: string;
  updatedAt: string;
  deletedAt: string | null;
}

export interface WorkTask {
  id: string;
  workItemId: string | null;
  title: string;
  detail: string;
  status: string;
  priority: string;
  plannedDate: string | null;
  dueAt: string | null;
  reminderAt: string | null;
  estimatedMinutes: number;
  actualMinutes: number;
  progress: number;
  rowVersion: number;
  createdAt: string;
  updatedAt: string;
  deletedAt: string | null;
}

export interface PendingMutation {
  mutationId: string;
  entityType: string;
  entityId: string;
  operation: string;
  baseRowVersion: number;
  payloadJson: string;
  clientChangedAt: string;
}

export interface SyncEnvelope {
  changeId: string;
  sequence: number;
  entityType: string;
  entityId: string;
  operation: string;
  rowVersion: number;
  payloadJson: string;
  changedAt: string;
}

export interface SyncConflict {
  mutationId: string;
  entityType: string;
  entityId: string;
  clientVersion: number;
  serverVersion: number;
  serverPayloadJson: string;
  reason: string;
}

export interface PullResponse {
  latestSequence: number;
  changes: SyncEnvelope[];
}

export interface PushResponse {
  latestSequence: number;
  acceptedMutationIds: string[];
  conflicts: SyncConflict[];
}

export interface SessionState {
  serverUrl: string;
  accessToken: string;
  expiresAt: string;
  userId: string;
  deviceId: string;
  displayName: string;
}

export interface SyncSummary {
  pushed: number;
  pulled: number;
  conflicts: number;
  latestSequence: number;
}
