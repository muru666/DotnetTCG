export interface SidePanelState {
  isOpen: boolean;
  activeTab: 'chat' | 'history';
  newMessageCount: number;
}
