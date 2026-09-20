import { UIAbility } from '@kit.AbilityKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { window } from '@kit.ArkUI';

export default class EntryAbility extends UIAbility {
  onWindowStageCreate(windowStage: window.WindowStage): void {
    windowStage.loadContent('pages/Index', (error) => {
      if (error.code) {
        hilog.error(0x0000, 'PersonalWorkBoard', '页面加载失败：%{public}s', JSON.stringify(error));
        return;
      }
      hilog.info(0x0000, 'PersonalWorkBoard', '主页面加载完成');
    });
  }
}
