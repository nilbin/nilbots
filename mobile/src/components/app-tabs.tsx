import { NativeTabs } from 'expo-router/unstable-native-tabs';

import { Arena } from '@/theme/arena';

export default function AppTabs() {
  return (
    <NativeTabs
      backgroundColor={Arena.panel}
      indicatorColor={Arena.edge}
      labelStyle={{ selected: { color: Arena.accent } }}>
      <NativeTabs.Trigger name="index">
        <NativeTabs.Trigger.Label>Ladder</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon
          src={require('@/assets/images/tabIcons/home.png')}
          renderingMode="template"
        />
      </NativeTabs.Trigger>

      <NativeTabs.Trigger name="bots">
        <NativeTabs.Trigger.Label>Bots</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon
          src={require('@/assets/images/tabIcons/explore.png')}
          renderingMode="template"
        />
      </NativeTabs.Trigger>
    </NativeTabs>
  );
}
