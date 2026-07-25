import { Pressable, StyleSheet, Text } from 'react-native';

import { Arena, Radius, Space } from '@/theme/arena';

/**
 * A toggle filter. A chip rather than the site's checkbox — a 14px checkbox is an
 * unpleasant touch target, and chips give a comfortable one without inventing a
 * different visual language.
 */
export function FilterChip({
  label,
  active,
  onToggle,
}: {
  label: string;
  active: boolean;
  onToggle: () => void;
}) {
  return (
    <Pressable
      onPress={onToggle}
      accessibilityRole="switch"
      accessibilityState={{ checked: active }}
      accessibilityLabel={label}
      style={({ pressed }) => [
        styles.chip,
        active && styles.active,
        pressed && styles.pressed,
      ]}>
      <Text style={[styles.label, active && styles.activeLabel]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  chip: {
    paddingHorizontal: Space.md,
    paddingVertical: 8,
    borderRadius: Radius.sm,
    borderWidth: 1,
    borderColor: Arena.edge,
    backgroundColor: Arena.bg,
  },
  active: { borderColor: Arena.accent, backgroundColor: `${Arena.accent}1a` },
  pressed: { opacity: 0.7 },
  label: { color: Arena.dim, fontSize: 13 },
  activeLabel: { color: Arena.accent, fontWeight: '600' },
});
