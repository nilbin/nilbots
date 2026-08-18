/** A single-choice row of small chips, accented per group where one applies. */
export function SegChips<T extends string>({ options, value, accent, onChange }: {
  options: { id: T; label: string }[];
  value: T;
  accent?: string;
  onChange: (id: T) => void;
}) {
  return (
    <div className="flex flex-wrap gap-1">
      {options.map((option) => {
        const on = option.id === value;
        return (
          <button
            key={option.id}
            type="button"
            onClick={() => onChange(option.id)}
            className={`min-h-8 rounded-sm border px-2.5 t-micro transition-colors ${on
              ? 'bg-arena-raise font-semibold text-arena-text'
              : 'border-arena-edge text-arena-dim hover:text-arena-text'}`}
            style={on ? {
              borderColor: `color-mix(in oklab, ${accent ?? 'var(--color-arena-text)'} 55%, var(--color-arena-edge2))`,
              boxShadow: `inset 0 -2px 0 ${accent ?? 'var(--color-arena-text)'}`,
            } : undefined}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
