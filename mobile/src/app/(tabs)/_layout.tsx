import AppTabs from '@/components/app-tabs';

/**
 * The tab bar. It sits inside the root stack rather than being the root itself, so a
 * match or a set can push over the whole app — those are reachable from a bot, from the
 * ladder, and eventually from an arena listing, and none of those should have to change
 * tabs to show one.
 */
export default function TabsLayout() {
  return <AppTabs />;
}
