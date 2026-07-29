import { useState } from 'react';
import type { BotDetail } from '../api';
import { botClassPresentation, orderBotClassIds } from '../botClasses';
import { errorMessage } from '../errorMessage';
import { useAssignBotClass, useMeta } from '../queries';

export default function BotClassCard({
  bot,
  botKey,
}: {
  bot: BotDetail;
  botKey: string;
}) {
  const assignment = useAssignBotClass(botKey, bot.id);
  const { data: meta, error: metaError } = useMeta(bot.isOwner && !bot.classId);
  const [classId, setClassId] = useState('');

  if (bot.classId) {
    const presentation = botClassPresentation(bot.classId);
    return (
      <section className="panel pad">
        <p className="lab mb-2">Class</p>
        <p className="t-body font-semibold text-arena-text">
          {presentation.label}
        </p>
        <p className="t-meta mt-1">{presentation.description}</p>
        <p className="t-micro mt-3">Chosen once for this bot.</p>
      </section>
    );
  }

  if (!bot.isOwner) {
    return (
      <section className="panel pad">
        <p className="lab mb-2">Class</p>
        <p className="t-meta">Unassigned legacy bot.</p>
      </section>
    );
  }

  const classes = orderBotClassIds(
    meta?.botClasses.map((entry) => entry.id) ?? [],
  );
  const selected = classId ? botClassPresentation(classId) : null;
  const assign = (event: React.FormEvent) => {
    event.preventDefault();
    if (!classId) return;
    assignment.mutate({ classId });
  };

  return (
    <section className="panel pad">
      <p className="lab mb-2">Choose class</p>
      <p className="t-meta mb-3">
        This legacy bot has no class yet. Assignment is permanent.
      </p>
      <form onSubmit={assign} className="flex flex-col gap-2">
        <label className="t-meta flex flex-col gap-1">
          Class
          <select
            className="field"
            value={classId}
            onChange={(event) => setClassId(event.target.value)}
            required
          >
            <option value="">Choose a class…</option>
            {classes.map((id) => (
              <option key={id} value={id}>
                {botClassPresentation(id).label}
              </option>
            ))}
          </select>
        </label>
        {selected && <p className="t-micro">{selected.description}</p>}
        {metaError && (
          <p className="t-body text-arena-hot">
            {errorMessage(metaError, 'Could not load classes.')}
          </p>
        )}
        {assignment.isError && (
          <p className="t-body text-arena-hot">
            {errorMessage(assignment.error, 'Could not assign class.')}
          </p>
        )}
        <button
          className="btn btn-strong mt-1 min-h-11 self-start disabled:opacity-40"
          type="submit"
          disabled={!classId || !meta || assignment.isPending}
        >
          {assignment.isPending ? 'Assigning…' : 'Assign class'}
        </button>
      </form>
    </section>
  );
}
