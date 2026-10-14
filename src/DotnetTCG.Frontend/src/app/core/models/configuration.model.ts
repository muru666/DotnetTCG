export type ThemeOption = 'dark-theme' | 'light-theme';

export interface SoundConfiguration {
  general: number;
  music: number;
  effects: number;
}

export interface AppConfiguration {
  showAnimations: boolean;
  theme: ThemeOption;
  sound: SoundConfiguration;
}

export const DEFAULT_CONFIGURATION: AppConfiguration = {
  showAnimations: true,
  theme: 'dark-theme',
  sound: {
    general: 50,
    music: 20,
    effects: 15
  }
};
