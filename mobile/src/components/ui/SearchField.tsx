import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

import { Arena, Radius, Space } from '@/theme/arena';

/** A filter box that matches the site's input treatment: hairline edge on the field colour. */
export function SearchField({
  value,
  onChange,
  placeholder,
  accessibilityLabel,
}: {
  value: string;
  onChange: (next: string) => void;
  placeholder?: string;
  accessibilityLabel?: string;
}) {
  return (
    <View style={styles.wrap}>
      <TextInput
        value={value}
        onChangeText={onChange}
        placeholder={placeholder}
        placeholderTextColor={Arena.dim}
        accessibilityLabel={accessibilityLabel}
        autoCapitalize="none"
        autoCorrect={false}
        clearButtonMode="never"
        returnKeyType="search"
        style={styles.input}
      />
      {value.length > 0 ? (
        <Pressable
          onPress={() => onChange('')}
          hitSlop={10}
          accessibilityRole="button"
          accessibilityLabel="Clear filter">
          <Text style={styles.clear}>✕</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Space.sm,
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.sm,
    backgroundColor: Arena.bg,
    paddingHorizontal: Space.md,
  },
  input: { flex: 1, color: Arena.text, fontSize: 14, paddingVertical: 10 },
  clear: { color: Arena.dim, fontSize: 13, paddingHorizontal: Space.xs },
});
