import { useEffect } from 'react';

/** The site's one sheet-editor modal: veil, panel, Escape to close. */
export function Modal({ onClose, children, wide = false }: {
  onClose: () => void;
  children: React.ReactNode;
  wide?: boolean;
}) {
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  return (
    <div
      className="fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-black/60 p-4 pt-[7vh] backdrop-blur-[2px]"
      onClick={onClose}
      role="presentation"
    >
      <div
        className={`panel pad w-full ${wide ? 'max-w-4xl' : 'max-w-xl'}`}
        onClick={(event) => event.stopPropagation()}
        role="dialog"
        aria-modal="true"
      >
        {children}
      </div>
    </div>
  );
}
