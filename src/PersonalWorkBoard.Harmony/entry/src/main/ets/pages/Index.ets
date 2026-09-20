import { util } from '@kit.ArkTS';
import { promptAction } from '@kit.ArkUI';
import { scanBarcode, scanCore } from '@kit.ScanKit';
import {
  LoginResponse,
  PairingResponse,
  SessionState,
  SyncConflict,
  SyncSummary,
  WorkItem,
  WorkTask
} from '../models/Contracts';
import { ApiClient } from '../services/ApiClient';
import { StorageService } from '../services/StorageService';
import { SyncService } from '../services/SyncService';

interface PairingData {
  serverUrl: string;
  ticket: string;
}

const PAGE_BACKGROUND: string = '#F4F7FB';
const CARD_BACKGROUND: string = '#FFFFFFFF';
const TEXT_PRIMARY: string = '#0F172A';
const TEXT_SECONDARY: string = '#64748B';
const BLUE: string = '#2563EB';
const BLUE_SOFT: string = '#E8F0FF';
const GREEN: string = '#16A34A';
const RED: string = '#DC2626';
const ORANGE: string = '#EA580C';

function localDate(): string {
  const now = new Date();
  const month = `${now.getMonth() + 1}`.padStart(2, '0');
  const day = `${now.getDate()}`.padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

function typeName(value: string): string {
  const names: Record<string, string> = {
    'Code': '代码',
    'Website': '网站',
    'ProductIdea': '产品创意',
    'NewProduct': '新品开发',
    'DailyWork': '日常工作',
    'General': '普通点子'
  };
  return names[value] ?? value;
}

function priorityName(value: string): string {
  const names: Record<string, string> = {
    'Low': '低',
    'Medium': '普通',
    'High': '高',
    'Urgent': '紧急'
  };
  return names[value] ?? value;
}

function priorityColor(value: string): string {
  if (value === 'Urgent') {
    return RED;
  }
  if (value === 'High') {
    return ORANGE;
  }
  if (value === 'Low') {
    return GREEN;
  }
  return BLUE;
}

@Entry
@Component
struct Index {
  private readonly storage: StorageService = StorageService.getInstance();
  private readonly api: ApiClient = ApiClient.getInstance();
  private readonly syncService: SyncService = SyncService.getInstance();

  @State private ready: boolean = false;
  @State private busy: boolean = false;
  @State private selectedTab: number = 0;
  @State private session: SessionState | null = null;
  @State private items: WorkItem[] = [];
  @State private tasks: WorkTask[] = [];
  @State private conflicts: SyncConflict[] = [];
  @State private serverUrl: string = 'http://192.168.1.100';
  @State private userName: string = 'admin';
  @State private password: string = '';
  @State private pairPayload: string = '';
  @State private newTaskTitle: string = '';
  @State private newTaskPriority: string = 'Medium';
  @State private newTaskMinutes: string = '30';
  @State private newItemTitle: string = '';
  @State private newItemDescription: string = '';
  @State private newItemType: string = 'General';
  @State private newItemNextAction: string = '';
  @State private parentProjectId: string | null = null;
  @State private lastSyncAt: string = '尚未同步';
  @State private syncMessage: string = '等待首次同步';
  @State private reminderMessage: string = '';
  @State private morningReminder: string = '08:30';
  @State private eveningReminder: string = '18:00';

  aboutToAppear(): void {
    this.initialize();
  }

  private async initialize(): Promise<void> {
    try {
      const context = this.getUIContext().getHostContext();
      if (context === undefined) {
        throw new Error('无法取得应用上下文');
      }
      await this.storage.initialize(context);
      this.serverUrl = await this.storage.getServerHint();
      this.morningReminder = await this.storage.getMorningReminder();
      this.eveningReminder = await this.storage.getEveningReminder();
      this.session = await this.storage.getSession();
      await this.reloadLocalData();
      this.updateReminderMessage();
      if (this.session !== null) {
        this.synchronize(false);
      }
    } catch (error) {
      this.showError(error);
    } finally {
      this.ready = true;
    }
  }

  private async reloadLocalData(): Promise<void> {
    this.items = (await this.storage.getItems()).filter((item: WorkItem) => item.deletedAt === null);
    this.tasks = (await this.storage.getTasks()).filter((task: WorkTask) => task.deletedAt === null);
    this.conflicts = await this.storage.getConflicts();
    this.lastSyncAt = await this.storage.getLastSyncAt();
  }

  private showError(error: Object): void {
    const message = error instanceof Error ? error.message : `${error}`;
    promptAction.showToast({ message: message, duration: 3500 });
  }

  private updateReminderMessage(): void {
    const hour = new Date().getHours();
    const unfinished = this.tasks.filter((task: WorkTask) =>
      task.plannedDate === localDate() && task.status !== 'Done' && task.status !== 'Cancelled').length;
    if (hour < 12) {
      this.reminderMessage = unfinished > 0 ? `早上好，今天已有 ${unfinished} 项工作等待推进` : '早上好，先安排今天最重要的一件事';
    } else if (hour >= 18) {
      this.reminderMessage = unfinished > 0 ? `今日还有 ${unfinished} 项未完成，记得复盘或顺延` : '今天的计划已收尾，可以记录复盘了';
    } else {
      this.reminderMessage = unfinished > 0 ? `今天还剩 ${unfinished} 项工作` : '今日任务已清空，保持节奏';
    }
  }

  private async login(): Promise<void> {
    if (this.busy || this.password.trim().length === 0) {
      return;
    }
    this.busy = true;
    try {
      const normalized = this.api.normalizeServerUrl(this.serverUrl);
      const response: LoginResponse = await this.api.login(normalized, this.userName.trim(), this.password);
      const session: SessionState = {
        serverUrl: normalized,
        accessToken: response.accessToken,
        expiresAt: response.expiresAt,
        userId: response.userId,
        deviceId: response.deviceId,
        displayName: response.displayName
      };
      await this.storage.saveSession(session);
      await this.storage.saveServerHint(normalized);
      this.session = session;
      this.password = '';
      await this.synchronize(false);
    } catch (error) {
      this.showError(error);
    } finally {
      this.busy = false;
    }
  }

  private parsePairingPayload(payload: string): PairingData {
    if (!payload.startsWith('pwb://pair?')) {
      throw new Error('这不是个人工作看板的配对二维码');
    }
    const query = payload.substring(payload.indexOf('?') + 1);
    let server = '';
    let ticket = '';
    query.split('&').forEach((part: string) => {
      const separator = part.indexOf('=');
      if (separator <= 0) {
        return;
      }
      const key = part.substring(0, separator);
      const value = decodeURIComponent(part.substring(separator + 1));
      if (key === 'server') {
        server = value;
      } else if (key === 'ticket') {
        ticket = value;
      }
    });
    if (server.length === 0 || ticket.length === 0) {
      throw new Error('二维码缺少服务器地址或一次性票据');
    }
    return { serverUrl: this.api.normalizeServerUrl(server), ticket: ticket };
  }

  private async scanPairingCode(): Promise<void> {
    try {
      const context = this.getUIContext().getHostContext();
      if (context === undefined) {
        throw new Error('无法打开扫码界面');
      }
      const options: scanBarcode.ScanOptions = {
        scanTypes: [scanCore.ScanType.ALL],
        enableMultiMode: false,
        enableAlbum: true
      };
      const result = await scanBarcode.startScanForResult(context, options);
      this.pairPayload = result.originalValue;
      await this.redeemPairingPayload();
    } catch (error) {
      this.showError(error);
    }
  }

  private async redeemPairingPayload(): Promise<void> {
    if (this.busy || this.pairPayload.trim().length === 0) {
      return;
    }
    this.busy = true;
    try {
      const pairing = this.parsePairingPayload(this.pairPayload.trim());
      const response: PairingResponse = await this.api.redeem(pairing.serverUrl, pairing.ticket);
      const session: SessionState = {
        serverUrl: pairing.serverUrl,
        accessToken: response.accessToken,
        expiresAt: response.expiresAt,
        userId: response.userId,
        deviceId: response.deviceId,
        displayName: response.serverName
      };
      await this.storage.saveSession(session);
      await this.storage.saveServerHint(pairing.serverUrl);
      this.serverUrl = pairing.serverUrl;
      this.session = session;
      this.pairPayload = '';
      await this.synchronize(false);
    } catch (error) {
      this.showError(error);
    } finally {
      this.busy = false;
    }
  }

  private async synchronize(showToast: boolean = true): Promise<void> {
    const activeSession = this.session;
    if (this.busy || activeSession === null) {
      return;
    }
    this.busy = true;
    this.syncMessage = '正在与局域网服务器同步…';
    try {
      const summary: SyncSummary = await this.syncService.synchronize(activeSession);
      await this.reloadLocalData();
      this.updateReminderMessage();
      this.syncMessage = `已上传 ${summary.pushed} 条，下载 ${summary.pulled} 条`;
      if (summary.conflicts > 0) {
        this.syncMessage += `，发现 ${summary.conflicts} 个冲突`;
      }
      if (showToast) {
        promptAction.showToast({ message: '同步完成' });
      }
    } catch (error) {
      this.syncMessage = '当前离线，修改已保存在手机';
      if (showToast) {
        this.showError(error);
      }
    } finally {
      this.busy = false;
    }
  }

  private async addTask(): Promise<void> {
    const title = this.newTaskTitle.trim();
    if (title.length === 0) {
      promptAction.showToast({ message: '先填写今日工作标题' });
      return;
    }
    const now = new Date().toISOString();
    let minutes = Number(this.newTaskMinutes);
    if (Number.isNaN(minutes) || minutes < 5) {
      minutes = 30;
    }
    const task: WorkTask = {
      id: util.generateRandomUUID(true),
      workItemId: null,
      title: title,
      detail: '',
      status: 'Todo',
      priority: this.newTaskPriority,
      plannedDate: localDate(),
      dueAt: null,
      reminderAt: null,
      estimatedMinutes: Math.min(minutes, 1440),
      actualMinutes: 0,
      progress: 0,
      rowVersion: 0,
      createdAt: now,
      updatedAt: now,
      deletedAt: null
    };
    try {
      await this.syncService.queueTask(task);
      this.newTaskTitle = '';
      await this.reloadLocalData();
      this.updateReminderMessage();
      this.syncMessage = '新增工作已保存，等待同步';
    } catch (error) {
      this.showError(error);
    }
  }

  private async completeTask(task: WorkTask): Promise<void> {
    const updated: WorkTask = {
      id: task.id,
      workItemId: task.workItemId,
      title: task.title,
      detail: task.detail,
      status: task.status === 'Done' ? 'Todo' : 'Done',
      priority: task.priority,
      plannedDate: task.plannedDate,
      dueAt: task.dueAt,
      reminderAt: task.reminderAt,
      estimatedMinutes: task.estimatedMinutes,
      actualMinutes: task.actualMinutes,
      progress: task.status === 'Done' ? 0 : 100,
      rowVersion: task.rowVersion,
      createdAt: task.createdAt,
      updatedAt: new Date().toISOString(),
      deletedAt: null
    };
    await this.syncService.queueTask(updated);
    await this.reloadLocalData();
    this.updateReminderMessage();
  }

  private async addWorkItem(): Promise<void> {
    const title = this.newItemTitle.trim();
    if (title.length === 0) {
      promptAction.showToast({ message: '先填写点子或项目名称' });
      return;
    }
    const now = new Date().toISOString();
    const item: WorkItem = {
      id: util.generateRandomUUID(true),
      parentId: this.parentProjectId,
      type: this.newItemType,
      status: 'Inbox',
      priority: 'Medium',
      title: title,
      description: this.newItemDescription.trim(),
      progress: 0,
      nextAction: this.newItemNextAction.trim(),
      targetDate: null,
      currentVersion: 'V0.1',
      rowVersion: 0,
      createdAt: now,
      updatedAt: now,
      deletedAt: null
    };
    try {
      await this.syncService.queueItem(item);
      this.newItemTitle = '';
      this.newItemDescription = '';
      this.newItemNextAction = '';
      this.parentProjectId = null;
      await this.reloadLocalData();
      this.syncMessage = '新项目已保存，等待同步';
    } catch (error) {
      this.showError(error);
    }
  }

  private async advanceItem(item: WorkItem): Promise<void> {
    const nextProgress = Math.min(100, item.progress + 10);
    const updated: WorkItem = {
      id: item.id,
      parentId: item.parentId,
      type: item.type,
      status: nextProgress === 100 ? 'Done' : 'InProgress',
      priority: item.priority,
      title: item.title,
      description: item.description,
      progress: nextProgress,
      nextAction: item.nextAction,
      targetDate: item.targetDate,
      currentVersion: item.currentVersion,
      rowVersion: item.rowVersion,
      createdAt: item.createdAt,
      updatedAt: new Date().toISOString(),
      deletedAt: null
    };
    await this.syncService.queueItem(updated);
    await this.reloadLocalData();
  }

  private prepareChildIdea(item: WorkItem): void {
    this.parentProjectId = item.id;
    this.newItemTitle = '';
    this.newItemDescription = `来源项目：${item.title}`;
    this.newItemType = 'ProductIdea';
    promptAction.showToast({ message: '已切换为子创意，请在顶部填写名称' });
  }

  private async saveReminderSettings(): Promise<void> {
    await this.storage.saveMorningReminder(this.morningReminder.trim());
    await this.storage.saveEveningReminder(this.eveningReminder.trim());
    promptAction.showToast({ message: '提醒时间已保存' });
  }

  private async logout(): Promise<void> {
    await this.storage.clearSession();
    this.session = null;
    this.selectedTab = 0;
  }

  @Builder
  private appMark(): void {
    Row() {
      Column() {
        Text('✓')
          .fontSize(24)
          .fontWeight(FontWeight.Bold)
          .fontColor(Color.White)
      }
      .width(50)
      .height(50)
      .justifyContent(FlexAlign.Center)
      .backgroundColor(BLUE)
      .borderRadius(16)

      Column() {
        Text('个人工作看板')
          .fontSize(22)
          .fontWeight(FontWeight.Bold)
          .fontColor(TEXT_PRIMARY)
        Text('HarmonyOS · 局域网同步')
          .fontSize(13)
          .fontColor(TEXT_SECONDARY)
          .margin({ top: 3 })
      }
      .alignItems(HorizontalAlign.Start)
      .margin({ left: 14 })
    }
  }

  @Builder
  private loginPage(): void {
    Scroll() {
      Column() {
        Blank().height(48)
        this.appMark()
        Text('把今天要做的事，变成看得见的进展。')
          .fontSize(16)
          .fontColor(TEXT_SECONDARY)
          .margin({ top: 18, bottom: 28 })

        Column() {
          Text('连接工作服务器')
            .fontSize(20)
            .fontWeight(FontWeight.Bold)
            .fontColor(TEXT_PRIMARY)
            .width('100%')
          Text('手机与 PC、Rocky Linux 服务器需在同一局域网')
            .fontSize(13)
            .fontColor(TEXT_SECONDARY)
            .width('100%')
            .margin({ top: 6, bottom: 18 })

          Text('服务器地址').fontSize(13).fontColor(TEXT_SECONDARY).width('100%')
          TextInput({ text: this.serverUrl, placeholder: 'http://192.168.1.100' })
            .onChange((value: string) => this.serverUrl = value)
            .height(48)
            .backgroundColor('#F8FAFC')
            .borderRadius(12)
            .margin({ top: 7, bottom: 14 })

          Text('用户名').fontSize(13).fontColor(TEXT_SECONDARY).width('100%')
          TextInput({ text: this.userName, placeholder: 'admin' })
            .onChange((value: string) => this.userName = value)
            .height(48)
            .backgroundColor('#F8FAFC')
            .borderRadius(12)
            .margin({ top: 7, bottom: 14 })

          Text('密码').fontSize(13).fontColor(TEXT_SECONDARY).width('100%')
          TextInput({ text: this.password, placeholder: '服务器管理员密码' })
            .type(InputType.Password)
            .onChange((value: string) => this.password = value)
            .height(48)
            .backgroundColor('#F8FAFC')
            .borderRadius(12)
            .margin({ top: 7, bottom: 18 })

          Button(this.busy ? '连接中…' : '登录并同步')
            .width('100%')
            .height(50)
            .backgroundColor(BLUE)
            .fontColor(Color.White)
            .fontWeight(FontWeight.Medium)
            .borderRadius(14)
            .enabled(!this.busy)
            .onClick(() => this.login())

          Row() {
            Divider().layoutWeight(1).color('#E2E8F0')
            Text('或使用 PC 端二维码').fontSize(12).fontColor(TEXT_SECONDARY).margin({ left: 12, right: 12 })
            Divider().layoutWeight(1).color('#E2E8F0')
          }
          .margin({ top: 22, bottom: 18 })

          Button('扫一扫配对')
            .width('100%')
            .height(48)
            .backgroundColor(BLUE_SOFT)
            .fontColor(BLUE)
            .borderRadius(14)
            .onClick(() => this.scanPairingCode())

          TextArea({ text: this.pairPayload, placeholder: '扫码不可用时，可粘贴 pwb://pair?... 配对内容' })
            .onChange((value: string) => this.pairPayload = value)
            .height(72)
            .backgroundColor('#F8FAFC')
            .borderRadius(12)
            .margin({ top: 12 })
          Button('使用粘贴内容配对')
            .width('100%')
            .height(42)
            .backgroundColor('#FFFFFF')
            .fontColor(BLUE)
            .border({ width: 1, color: '#BFDBFE' })
            .borderRadius(12)
            .margin({ top: 8 })
            .onClick(() => this.redeemPairingPayload())
        }
        .width('100%')
        .padding(22)
        .backgroundColor(CARD_BACKGROUND)
        .borderRadius(22)
        .shadow({ radius: 18, color: '#120F172A', offsetX: 0, offsetY: 6 })

        Text('仅连接你自己的局域网服务器；配对二维码 5 分钟内有效且只能使用一次。')
          .fontSize(12)
          .fontColor(TEXT_SECONDARY)
          .textAlign(TextAlign.Center)
          .margin({ top: 20, bottom: 30 })
      }
      .width('100%')
      .padding({ left: 20, right: 20 })
    }
    .width('100%')
    .height('100%')
    .backgroundColor(PAGE_BACKGROUND)
  }

  @Builder
  private pageHeader(title: string, subtitle: string): void {
    Row() {
      Column() {
        Text(title).fontSize(25).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
        Text(subtitle).fontSize(13).fontColor(TEXT_SECONDARY).margin({ top: 3 })
      }
      .alignItems(HorizontalAlign.Start)
      .layoutWeight(1)
      Button(this.busy ? '同步中' : '同步')
        .height(38)
        .backgroundColor(BLUE_SOFT)
        .fontColor(BLUE)
        .borderRadius(12)
        .enabled(!this.busy)
        .onClick(() => this.synchronize())
    }
    .width('100%')
    .padding({ left: 20, right: 20, top: 16, bottom: 14 })
    .backgroundColor(PAGE_BACKGROUND)
  }

  @Builder
  private priorityChip(value: string): void {
    Text(priorityName(value))
      .fontSize(12)
      .fontWeight(FontWeight.Medium)
      .fontColor(this.newTaskPriority === value ? Color.White : priorityColor(value))
      .padding({ left: 13, right: 13, top: 7, bottom: 7 })
      .backgroundColor(this.newTaskPriority === value ? priorityColor(value) : '#F1F5F9')
      .borderRadius(18)
      .onClick(() => this.newTaskPriority = value)
  }

  @Builder
  private todayPage(): void {
    Column() {
      this.pageHeader('今日工作', `${localDate()} · ${this.lastSyncAt}`)
      Scroll() {
        Column() {
          Row() {
            Text('☀').fontSize(22).margin({ right: 10 })
            Text(this.reminderMessage)
              .fontSize(14)
              .fontColor('#1E3A8A')
              .layoutWeight(1)
          }
          .width('100%')
          .padding(16)
          .backgroundColor('#DBEAFE')
          .borderRadius(16)
          .margin({ bottom: 14 })

          Column() {
            Text('快速添加今日工作')
              .width('100%')
              .fontSize(17)
              .fontWeight(FontWeight.Bold)
              .fontColor(TEXT_PRIMARY)
            TextInput({ text: this.newTaskTitle, placeholder: '例如：完成登录页联调' })
              .onChange((value: string) => this.newTaskTitle = value)
              .height(48)
              .backgroundColor('#F8FAFC')
              .borderRadius(12)
              .margin({ top: 12, bottom: 10 })
            Row({ space: 7 }) {
              this.priorityChip('Low')
              this.priorityChip('Medium')
              this.priorityChip('High')
              this.priorityChip('Urgent')
            }
            .width('100%')
            Row() {
              TextInput({ text: this.newTaskMinutes, placeholder: '30' })
                .type(InputType.Number)
                .onChange((value: string) => this.newTaskMinutes = value)
                .height(44)
                .width(100)
                .backgroundColor('#F8FAFC')
                .borderRadius(12)
              Text('分钟').fontSize(13).fontColor(TEXT_SECONDARY).margin({ left: 8 })
              Blank()
              Button('加入今天')
                .height(44)
                .backgroundColor(BLUE)
                .fontColor(Color.White)
                .borderRadius(12)
                .onClick(() => this.addTask())
            }
            .width('100%')
            .margin({ top: 12 })
          }
          .width('100%')
          .padding(17)
          .backgroundColor(CARD_BACKGROUND)
          .borderRadius(18)

          Row() {
            Text('今日清单').fontSize(18).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
            Blank()
            Text(`${this.todayTasks().filter((task: WorkTask) => task.status === 'Done').length}/${this.todayTasks().length}`)
              .fontSize(13).fontColor(TEXT_SECONDARY)
          }
          .width('100%')
          .margin({ top: 22, bottom: 10 })

          if (this.todayTasks().length === 0) {
            Column() {
              Text('今天还没有工作安排').fontSize(15).fontWeight(FontWeight.Medium).fontColor(TEXT_PRIMARY)
              Text('先写下一件可以立刻开始的小事').fontSize(13).fontColor(TEXT_SECONDARY).margin({ top: 6 })
            }
            .width('100%')
            .padding(28)
            .backgroundColor(CARD_BACKGROUND)
            .borderRadius(18)
          }

          ForEach(this.todayTasks(), (task: WorkTask) => {
            Row() {
              Button(task.status === 'Done' ? '✓' : '')
                .width(34)
                .height(34)
                .fontSize(18)
                .fontColor(Color.White)
                .backgroundColor(task.status === 'Done' ? GREEN : '#FFFFFF')
                .border({ width: 2, color: task.status === 'Done' ? GREEN : '#CBD5E1' })
                .borderRadius(17)
                .onClick(() => this.completeTask(task))
              Column() {
                Text(task.title)
                  .fontSize(15)
                  .fontWeight(FontWeight.Medium)
                  .fontColor(task.status === 'Done' ? '#94A3B8' : TEXT_PRIMARY)
                  .decoration({ type: task.status === 'Done' ? TextDecorationType.LineThrough : TextDecorationType.None })
                Row() {
                  Text(priorityName(task.priority)).fontSize(11).fontColor(priorityColor(task.priority))
                  Text(` · ${task.estimatedMinutes} 分钟`).fontSize(11).fontColor(TEXT_SECONDARY)
                  if (task.workItemId !== null) {
                    Text(' · 已关联项目').fontSize(11).fontColor(TEXT_SECONDARY)
                  }
                }
                .margin({ top: 5 })
              }
              .alignItems(HorizontalAlign.Start)
              .layoutWeight(1)
              .margin({ left: 12 })
            }
            .width('100%')
            .padding(15)
            .backgroundColor(CARD_BACKGROUND)
            .borderRadius(16)
            .margin({ bottom: 9 })
          }, (task: WorkTask) => task.id)
          Blank().height(24)
        }
        .width('100%')
        .padding({ left: 16, right: 16 })
      }
      .layoutWeight(1)
      .scrollBar(BarState.Off)
    }
    .width('100%')
    .height('100%')
    .backgroundColor(PAGE_BACKGROUND)
  }

  private todayTasks(): WorkTask[] {
    return this.tasks.filter((task: WorkTask) => task.plannedDate === localDate());
  }

  @Builder
  private typeChip(value: string): void {
    Text(typeName(value))
      .fontSize(12)
      .fontColor(this.newItemType === value ? Color.White : TEXT_SECONDARY)
      .padding({ left: 12, right: 12, top: 7, bottom: 7 })
      .backgroundColor(this.newItemType === value ? BLUE : '#F1F5F9')
      .borderRadius(17)
      .margin({ right: 7, bottom: 7 })
      .onClick(() => this.newItemType = value)
  }

  @Builder
  private boardPage(): void {
    Column() {
      this.pageHeader('工作看板', `${this.items.length} 个点子与项目`)
      Scroll() {
        Column() {
          Column() {
            Row() {
              Text(this.parentProjectId === null ? '记录新点子 / 项目' : '添加项目内的子创意')
                .fontSize(17).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
              Blank()
              if (this.parentProjectId !== null) {
                Text('取消').fontSize(13).fontColor(BLUE).onClick(() => this.parentProjectId = null)
              }
            }
            .width('100%')
            TextInput({ text: this.newItemTitle, placeholder: '点子或项目名称' })
              .onChange((value: string) => this.newItemTitle = value)
              .height(46).backgroundColor('#F8FAFC').borderRadius(12).margin({ top: 12 })
            TextArea({ text: this.newItemDescription, placeholder: '背景、目标或验收标准' })
              .onChange((value: string) => this.newItemDescription = value)
              .height(70).backgroundColor('#F8FAFC').borderRadius(12).margin({ top: 9 })
            TextInput({ text: this.newItemNextAction, placeholder: '下一步行动' })
              .onChange((value: string) => this.newItemNextAction = value)
              .height(44).backgroundColor('#F8FAFC').borderRadius(12).margin({ top: 9 })
            Flex({ wrap: FlexWrap.Wrap }) {
              this.typeChip('Code')
              this.typeChip('Website')
              this.typeChip('ProductIdea')
              this.typeChip('NewProduct')
              this.typeChip('DailyWork')
              this.typeChip('General')
            }
            .width('100%')
            .margin({ top: 11 })
            Button(this.parentProjectId === null ? '保存到看板' : '加入项目进程')
              .width('100%').height(45).backgroundColor(BLUE).fontColor(Color.White).borderRadius(12)
              .margin({ top: 13 }).onClick(() => this.addWorkItem())
          }
          .width('100%').padding(17).backgroundColor(CARD_BACKGROUND).borderRadius(18)

          Text('全部项目').width('100%').fontSize(18).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
            .margin({ top: 22, bottom: 10 })

          if (this.items.length === 0) {
            Text('还没有项目。从上方记录第一个点子，手机离线也能保存。')
              .width('100%').fontSize(14).fontColor(TEXT_SECONDARY).padding(24)
              .backgroundColor(CARD_BACKGROUND).borderRadius(18)
          }

          ForEach(this.items, (item: WorkItem) => {
            Column() {
              Row() {
                Text(typeName(item.type))
                  .fontSize(11).fontColor(BLUE).padding({ left: 9, right: 9, top: 5, bottom: 5 })
                  .backgroundColor(BLUE_SOFT).borderRadius(12)
                if (item.parentId !== null) {
                  Text('子创意').fontSize(11).fontColor('#7C3AED').margin({ left: 8 })
                }
                Blank()
                Text(item.currentVersion).fontSize(11).fontColor(TEXT_SECONDARY)
              }
              .width('100%')
              Text(item.title).width('100%').fontSize(17).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
                .margin({ top: 11 })
              if (item.description.length > 0) {
                Text(item.description).width('100%').fontSize(13).fontColor(TEXT_SECONDARY)
                  .maxLines(2).textOverflow({ overflow: TextOverflow.Ellipsis }).margin({ top: 6 })
              }
              Row() {
                Progress({ value: item.progress, total: 100, type: ProgressType.Linear })
                  .height(7).color(item.progress === 100 ? GREEN : BLUE).backgroundColor('#E2E8F0').layoutWeight(1)
                Text(`${item.progress}%`).fontSize(12).fontColor(TEXT_SECONDARY).margin({ left: 10 })
              }
              .width('100%').margin({ top: 14 })
              if (item.nextAction.length > 0) {
                Text(`下一步：${item.nextAction}`).width('100%').fontSize(12).fontColor('#334155').margin({ top: 8 })
              }
              Row() {
                Button(item.progress >= 100 ? '已完成' : '推进 10%')
                  .height(36).backgroundColor(item.progress >= 100 ? '#DCFCE7' : BLUE_SOFT)
                  .fontColor(item.progress >= 100 ? GREEN : BLUE).borderRadius(10)
                  .enabled(item.progress < 100).onClick(() => this.advanceItem(item))
                Button('新增子创意')
                  .height(36).backgroundColor('#F3E8FF').fontColor('#7C3AED').borderRadius(10)
                  .margin({ left: 8 }).onClick(() => this.prepareChildIdea(item))
              }
              .width('100%').margin({ top: 13 })
            }
            .width('100%').padding(17).backgroundColor(CARD_BACKGROUND).borderRadius(18).margin({ bottom: 10 })
          }, (item: WorkItem) => item.id)
          Blank().height(24)
        }
        .width('100%').padding({ left: 16, right: 16 })
      }
      .layoutWeight(1).scrollBar(BarState.Off)
    }
    .width('100%').height('100%').backgroundColor(PAGE_BACKGROUND)
  }

  @Builder
  private syncPage(): void {
    Column() {
      this.pageHeader('同步中心', 'PC、手机与服务器保持一致')
      Scroll() {
        Column() {
          Column() {
            Row() {
              Column() {
                Text('当前服务器').fontSize(12).fontColor(TEXT_SECONDARY)
                Text(this.session?.serverUrl ?? '').fontSize(15).fontWeight(FontWeight.Medium).fontColor(TEXT_PRIMARY)
                  .margin({ top: 5 })
              }.alignItems(HorizontalAlign.Start).layoutWeight(1)
              Text('已连接').fontSize(12).fontColor(GREEN).padding({ left: 10, right: 10, top: 6, bottom: 6 })
                .backgroundColor('#DCFCE7').borderRadius(14)
            }.width('100%')
            Divider().color('#E2E8F0').margin({ top: 16, bottom: 16 })
            Text(this.syncMessage).width('100%').fontSize(15).fontWeight(FontWeight.Medium).fontColor(TEXT_PRIMARY)
            Text(`最近同步：${this.lastSyncAt}`).width('100%').fontSize(12).fontColor(TEXT_SECONDARY).margin({ top: 5 })
            Button(this.busy ? '正在同步…' : '立即双向同步')
              .width('100%').height(48).backgroundColor(BLUE).fontColor(Color.White).borderRadius(13)
              .margin({ top: 18 }).enabled(!this.busy).onClick(() => this.synchronize())
          }
          .width('100%').padding(18).backgroundColor(CARD_BACKGROUND).borderRadius(18)

          Row() {
            Column() {
              Text(`${this.items.length}`).fontSize(24).fontWeight(FontWeight.Bold).fontColor(BLUE)
              Text('项目').fontSize(12).fontColor(TEXT_SECONDARY).margin({ top: 3 })
            }.layoutWeight(1)
            Column() {
              Text(`${this.tasks.length}`).fontSize(24).fontWeight(FontWeight.Bold).fontColor(GREEN)
              Text('工作').fontSize(12).fontColor(TEXT_SECONDARY).margin({ top: 3 })
            }.layoutWeight(1)
            Column() {
              Text(`${this.conflicts.length}`).fontSize(24).fontWeight(FontWeight.Bold)
                .fontColor(this.conflicts.length > 0 ? RED : TEXT_SECONDARY)
              Text('冲突').fontSize(12).fontColor(TEXT_SECONDARY).margin({ top: 3 })
            }.layoutWeight(1)
          }
          .width('100%').padding(18).backgroundColor(CARD_BACKGROUND).borderRadius(18).margin({ top: 12 })

          if (this.conflicts.length > 0) {
            Text('待处理冲突').width('100%').fontSize(18).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
              .margin({ top: 22, bottom: 10 })
            ForEach(this.conflicts, (conflict: SyncConflict) => {
              Column() {
                Text(`${conflict.entityType} · ${conflict.entityId.substring(0, 8)}`)
                  .width('100%').fontSize(14).fontWeight(FontWeight.Medium).fontColor(TEXT_PRIMARY)
                Text(conflict.reason).width('100%').fontSize(12).fontColor(RED).margin({ top: 6 })
                Text(`手机版本 ${conflict.clientVersion} / 服务器版本 ${conflict.serverVersion}`)
                  .width('100%').fontSize(11).fontColor(TEXT_SECONDARY).margin({ top: 5 })
              }
              .width('100%').padding(15).backgroundColor('#FEF2F2').borderRadius(14).margin({ bottom: 8 })
            }, (conflict: SyncConflict) => conflict.mutationId)
          }

          Column() {
            Text('同步规则').width('100%').fontSize(16).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
            Text('• 手机离线时先保存到应用私有空间\n• 回到同一局域网后手动或启动时同步\n• 同一条记录被两端修改时保留冲突，不静默覆盖')
              .width('100%').fontSize(13).fontColor(TEXT_SECONDARY).lineHeight(22).margin({ top: 10 })
          }
          .width('100%').padding(17).backgroundColor(CARD_BACKGROUND).borderRadius(18).margin({ top: 12, bottom: 24 })
        }
        .width('100%').padding({ left: 16, right: 16 })
      }
      .layoutWeight(1).scrollBar(BarState.Off)
    }
    .width('100%').height('100%').backgroundColor(PAGE_BACKGROUND)
  }

  @Builder
  private settingsPage(): void {
    Column() {
      this.pageHeader('设置', '个人工作看板 HarmonyOS 版')
      Scroll() {
        Column() {
          Column() {
            Text('每日工作提醒').width('100%').fontSize(17).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
            Text('当前版本在打开应用时根据时间和未完成任务给出计划/复盘提醒。')
              .width('100%').fontSize(13).fontColor(TEXT_SECONDARY).lineHeight(20).margin({ top: 7, bottom: 15 })
            Row() {
              Column() {
                Text('早间计划').fontSize(12).fontColor(TEXT_SECONDARY)
                TextInput({ text: this.morningReminder, placeholder: '08:30' })
                  .onChange((value: string) => this.morningReminder = value)
                  .width(120).height(43).backgroundColor('#F8FAFC').borderRadius(11).margin({ top: 6 })
              }.alignItems(HorizontalAlign.Start)
              Blank()
              Column() {
                Text('晚间复盘').fontSize(12).fontColor(TEXT_SECONDARY)
                TextInput({ text: this.eveningReminder, placeholder: '18:00' })
                  .onChange((value: string) => this.eveningReminder = value)
                  .width(120).height(43).backgroundColor('#F8FAFC').borderRadius(11).margin({ top: 6 })
              }.alignItems(HorizontalAlign.Start)
            }.width('100%')
            Button('保存提醒时间').width('100%').height(44).backgroundColor(BLUE_SOFT).fontColor(BLUE)
              .borderRadius(12).margin({ top: 14 }).onClick(() => this.saveReminderSettings())
          }
          .width('100%').padding(18).backgroundColor(CARD_BACKGROUND).borderRadius(18)

          Column() {
            Text('账号与设备').width('100%').fontSize(17).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
            Text(`显示名称：${this.session?.displayName ?? '-'}`).width('100%').fontSize(13).fontColor(TEXT_SECONDARY)
              .margin({ top: 12 })
            Text(`设备编号：${this.session?.deviceId.substring(0, 8) ?? '-'}`).width('100%')
              .fontSize(13).fontColor(TEXT_SECONDARY).margin({ top: 7 })
            Button('退出当前服务器')
              .width('100%').height(44).backgroundColor('#FEF2F2').fontColor(RED).borderRadius(12)
              .margin({ top: 16 }).onClick(() => this.logout())
          }
          .width('100%').padding(18).backgroundColor(CARD_BACKGROUND).borderRadius(18).margin({ top: 12 })

          Column() {
            Text('关于').width('100%').fontSize(17).fontWeight(FontWeight.Bold).fontColor(TEXT_PRIMARY)
            Text('版本 0.3.0 · 面向 HarmonyOS 7\n原生 ArkTS / ArkUI · 不依赖 Android 兼容层')
              .width('100%').fontSize(13).fontColor(TEXT_SECONDARY).lineHeight(22).margin({ top: 10 })
          }
          .width('100%').padding(18).backgroundColor(CARD_BACKGROUND).borderRadius(18).margin({ top: 12, bottom: 24 })
        }
        .width('100%').padding({ left: 16, right: 16 })
      }
      .layoutWeight(1).scrollBar(BarState.Off)
    }
    .width('100%').height('100%').backgroundColor(PAGE_BACKGROUND)
  }

  @Builder
  private tabBar(label: string, icon: string, index: number): void {
    Column() {
      Text(icon).fontSize(20).fontColor(this.selectedTab === index ? BLUE : '#94A3B8')
      Text(label).fontSize(11).fontColor(this.selectedTab === index ? BLUE : '#94A3B8').margin({ top: 2 })
    }
    .width('100%').height(54).justifyContent(FlexAlign.Center)
  }

  @Builder
  private workspace(): void {
    Tabs({ barPosition: BarPosition.End, index: this.selectedTab }) {
      TabContent() {
        this.todayPage()
      }.tabBar(this.tabBar('今日', '☑', 0))
      TabContent() {
        this.boardPage()
      }.tabBar(this.tabBar('看板', '▦', 1))
      TabContent() {
        this.syncPage()
      }.tabBar(this.tabBar('同步', '↻', 2))
      TabContent() {
        this.settingsPage()
      }.tabBar(this.tabBar('设置', '⚙', 3))
    }
    .onChange((index: number) => this.selectedTab = index)
    .barHeight(62)
    .barBackgroundColor('#FFFFFFFF')
    .width('100%')
    .height('100%')
    .backgroundColor(PAGE_BACKGROUND)
  }

  build() {
    Stack() {
      if (!this.ready) {
        Column() {
          this.appMark()
          LoadingProgress().width(36).height(36).color(BLUE).margin({ top: 30 })
        }
        .width('100%').height('100%').justifyContent(FlexAlign.Center).backgroundColor(PAGE_BACKGROUND)
      } else if (this.session === null) {
        this.loginPage()
      } else {
        this.workspace()
      }
    }
    .width('100%')
    .height('100%')
    .backgroundColor(PAGE_BACKGROUND)
  }
}
