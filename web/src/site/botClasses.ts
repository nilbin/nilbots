export interface BotClassPresentation {
  label: string;
  description: string;
}

const BOT_CLASS_PRESENTATION: Readonly<Record<string, BotClassPresentation>> = {
  striker: {
    label: 'Striker',
    description: 'Mobile pressure with committed volleys and bent shots.',
  },
  bulwark: {
    label: 'Bulwark',
    description: 'Fortifies into defensive forms and deflects incoming fire.',
  },
  fabricator: {
    label: 'Fabricator',
    description: 'Fabricates separate instances of itself to widen its formation.',
  },
};

export function botClassPresentation(classId: string): BotClassPresentation {
  return (
    BOT_CLASS_PRESENTATION[classId] ?? {
      label: classId,
      description: 'A registered Nilbots class.',
    }
  );
}

const CLASS_ORDER = ['striker', 'bulwark', 'fabricator'] as const;

export function orderBotClassIds(classIds: readonly string[]): string[] {
  const order = new Map(CLASS_ORDER.map((id, index) => [id, index]));
  return [...classIds].sort(
    (left, right) =>
      (order.get(left as (typeof CLASS_ORDER)[number]) ?? CLASS_ORDER.length) -
        (order.get(right as (typeof CLASS_ORDER)[number]) ?? CLASS_ORDER.length) ||
      left.localeCompare(right),
  );
}
