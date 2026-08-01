import ToggleButton from './ToggleButton';

/**
 * Whether the camera follows the fight.
 *
 * A toggle rather than an option: the camera is on by default because a whole arena on a
 * phone puts the actual fight inside a tenth of the screen, and it is switchable because
 * a spectator reading positions — where the frontline sits, who is flanking wide — wants
 * the fixed plan view the viewer has always had, and would otherwise have to fight the
 * camera for it with a gesture every few seconds.
 *
 * It also reports the override. Pan or zoom the arena and auto-fit stops; this is what
 * says so, and the only way back.
 */
export default function CameraFitToggle({
  enabled,
  onToggle,
}: {
  enabled: boolean;
  onToggle: () => void;
}) {
  return (
    <ToggleButton
      pressed={enabled}
      onClick={onToggle}
      className="val flex-none px-[9px] py-[5px]"
      description={
        enabled
          ? 'The camera follows the action. Turn off for the whole arena.'
          : 'The camera holds the whole arena. Turn on to follow the action.'
      }
    >
      {enabled ? '◉ director' : '▦ overview'}
    </ToggleButton>
  );
}
