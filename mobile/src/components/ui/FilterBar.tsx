import { SymbolView } from 'expo-symbols';
import { Platform, Pressable, StyleSheet, Text, View } from 'react-native';

import { SearchField } from '@/components/ui/SearchField';
import { Arena, Radius, Space } from '@/theme/arena';

/**
 * One line: a search field and a button for everything else.
 *
 * The app-wide rule this implements — a control bar never takes a second row. Extra
 * filters go behind the button, into a `BottomSheet`. A stacked bar eats the top of a
 * short screen and makes a list look like a form; on a phone, list rows are the content.
 *
 * `activeCount` badges the button so a filter that is on is visible without opening the
 * sheet — otherwise a hidden filter silently explains a short list.
 */
export function FilterBar({
  query,
  onQueryChange,
  placeholder,
  accessibilityLabel,
  activeCount = 0,
  onOpenFilters,
}: {
  query: string;
  onQueryChange: (next: string) => void;
  placeholder?: string;
  accessibilityLabel?: string;
  activeCount?: number;
  onOpenFilters?: () => void;
}) {
  return (
    <View style={styles.row}>
      <View style={styles.search}>
        <SearchField
          value={query}
          onChange={onQueryChange}
          placeholder={placeholder}
          accessibilityLabel={accessibilityLabel}
        />
      </View>
      {onOpenFilters ? (
        <Pressable
          onPress={onOpenFilters}
          accessibilityRole="button"
          accessibilityLabel={
            activeCount > 0 ? `Filters, ${activeCount} active` : 'Filters'
          }
          style={({ pressed }) => [
            styles.button,
            activeCount > 0 && styles.buttonActive,
            pressed && styles.pressed,
          ]}>
          <SymbolView
            name="line.3.horizontal.decrease"
            size={18}
            tintColor={activeCount > 0 ? Arena.accent : Arena.dim}
            // SF Symbols are iOS-only; Android and web get a plain glyph.
            fallback={
              <Text style={[styles.glyph, activeCount > 0 && styles.glyphActive]}>≡</Text>
            }
          />
          {activeCount > 0 ? (
            <View style={styles.badge}>
              <Text style={styles.badgeText}>{activeCount}</Text>
            </View>
          ) : null}
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'stretch', gap: Space.sm },
  search: { flex: 1 },
  button: {
    width: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.sm,
    backgroundColor: Arena.bg,
  },
  buttonActive: { borderColor: Arena.accent, backgroundColor: `${Arena.accent}1a` },
  pressed: { opacity: 0.7 },
  glyph: { color: Arena.dim, fontSize: 18, ...Platform.select({ default: {} }) },
  glyphActive: { color: Arena.accent },
  badge: {
    position: 'absolute',
    top: 4,
    right: 4,
    minWidth: 14,
    height: 14,
    borderRadius: 7,
    paddingHorizontal: 3,
    backgroundColor: Arena.accent,
    alignItems: 'center',
    justifyContent: 'center',
  },
  badgeText: { color: Arena.bg, fontSize: 9, fontWeight: '800' },
});
