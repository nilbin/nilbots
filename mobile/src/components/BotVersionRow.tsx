import { StyleSheet, Text, View } from 'react-native';

import { Card } from '@/components/ui/Card';
import type { BotVersion } from '@/api/client';
import { Arena, Mono, Space } from '@/theme/arena';

/** Build status is a server-side enum name; colour it rather than restating it. */
const STATUS_COLOUR: Record<string, string> = {
  Built: '#22c55e',
  Failed: '#ef4444',
  Building: Arena.accent,
  Queued: Arena.dim,
};

export function BotVersionRow({ version }: { version: BotVersion }) {
  return (
    <Card>
      <View style={styles.row}>
        <Text style={styles.number}>v{version.versionNumber}</Text>
        <View style={styles.middle}>
          <Text style={[styles.status, { color: STATUS_COLOUR[version.status] ?? Arena.dim }]}>
            {version.status.toLowerCase()}
            {version.isActive ? ' · active' : ''}
          </Text>
          {/* Owner-only fields are null for everyone else; the hash is the public one. */}
          {version.artifactHash ? (
            <Text style={styles.hash} numberOfLines={1}>
              {version.artifactHash.slice(0, 16)}…
            </Text>
          ) : null}
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  number: { ...Mono, color: Arena.text, fontSize: 14, fontWeight: '700', minWidth: 36 },
  middle: { flex: 1, gap: 2 },
  status: { fontSize: 13, fontWeight: '600' },
  hash: { ...Mono, color: Arena.dim, fontSize: 11 },
});
