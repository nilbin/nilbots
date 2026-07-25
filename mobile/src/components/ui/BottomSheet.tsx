import { BottomSheet as NativeBottomSheet } from '@expo/ui/community/bottom-sheet';
import type { ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { Arena, SectionLabelText, Space } from '@/theme/arena';

/**
 * A sheet that slides from the bottom for secondary controls — filters, sorts, actions.
 *
 * Wraps `@expo/ui`'s BottomSheet, which is a real SwiftUI sheet on iOS and a Material 3
 * ModalBottomSheet on Android, rather than a React Modal pretending to be one. That buys
 * native detents, momentum, and swipe-to-dismiss for free.
 *
 * Two things about that library worth knowing:
 *  - Its props are `@gorhom/bottom-sheet`-compatible: visibility is `index` (-1 closed),
 *    not an `isPresented` boolean. The published docs page shows the other shape; the
 *    installed types are the source of truth.
 *  - `@expo/ui` is alpha and documented as prone to breaking changes. It is contained
 *    behind this one component precisely so an upgrade is a single file to fix.
 *
 * The app's rule: a control bar stays one line, and anything that does not fit lives
 * behind a button that opens one of these.
 */
export function BottomSheet({
  visible,
  onClose,
  title,
  children,
}: {
  visible: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
}) {
  return (
    <NativeBottomSheet
      index={visible ? 0 : -1}
      enablePanDownToClose
      onClose={onClose}
      backgroundStyle={styles.background}>
      <View style={styles.body}>
        <View style={styles.header}>
          <Text style={styles.title}>{title}</Text>
          <Pressable onPress={onClose} hitSlop={12} accessibilityRole="button">
            <Text style={styles.done}>Done</Text>
          </Pressable>
        </View>
        {children}
      </View>
    </NativeBottomSheet>
  );
}

const styles = StyleSheet.create({
  // Only backgroundColor is honoured on native — it maps to SwiftUI's
  // .presentationBackground and Android's containerColor.
  background: { backgroundColor: Arena.panel },
  body: { padding: Space.lg, paddingBottom: Space.xxl, gap: Space.lg },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  title: SectionLabelText,
  done: { color: Arena.accent, fontSize: 15, fontWeight: '600' },
});
