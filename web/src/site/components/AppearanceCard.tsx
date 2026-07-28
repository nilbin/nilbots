import { Link } from 'react-router-dom';
import IdentityChip from '../../components/IdentityChip';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import type { BotDetail } from '../api';

export default function AppearanceCard({
  bot,
}: {
  bot: Pick<
    BotDetail,
    'name' | 'slug' | 'accent' | 'lookId' | 'projectileLookId'
  >;
}) {
  const chassis = botLook(bot.lookId);
  const projectile = projectileLook(bot.projectileLookId);

  return (
    <section className="panel pad">
      <h2 className="lab mb-2">Appearance</h2>
      <IdentityChip
        name={bot.name}
        accent={bot.accent}
        lookId={bot.lookId}
        size={24}
        className="max-w-full"
      />
      <p className="t-meta mt-2 flex flex-wrap items-center gap-2">
        <ProjectilePreview
          look={projectile}
          accent={bot.accent}
          className="h-6 w-12"
        />
        <span>
          {chassis.label} · {projectile.label}
        </span>
      </p>
      <Link
        to={`/bots/${bot.slug}/appearance`}
        className="btn mt-3 inline-flex w-full justify-center"
      >
        Choose appearance
      </Link>
    </section>
  );
}
