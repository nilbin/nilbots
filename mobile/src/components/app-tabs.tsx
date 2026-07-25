import { NativeTabs } from 'expo-router/unstable-native-tabs';

import { Arena } from '@/theme/arena';

export default function AppTabs() {
  return (
    <NativeTabs
      backgroundColor={Arena.panel}
      indicatorColor={Arena.edge}
      // iOS makes the tab bar transparent at a list's scroll edge and switches to a
      // material once content passes under it, so the bar visibly changed shade between
      // screens. Pin it to one appearance instead.
      disableTransparentOnScrollEdge
      tintColor={Arena.accent}
      labelStyle={{ color: Arena.dim, selected: { color: Arena.accent } }}>
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
