import { useEffect } from 'react';
import type { JSX, ReactNode } from 'react';

type ModalProps = {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
};

const Modal = ({ open, onClose, title, children }: ModalProps): JSX.Element | null => {
  useEffect(() => {
    if (!open) {
      return;
    }

    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [open, onClose]);

  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center bg-slate-900/60 p-4 pt-10 sm:items-center" onClick={onClose}>
      <div className="w-full max-w-3xl rounded-xl border border-border bg-surface shadow-xl" onClick={(event) => event.stopPropagation()} role="dialog" aria-modal="true" aria-label={title ?? 'Dialog'}>
        {title ? <div className="border-b border-border px-5 py-3 text-base font-semibold text-text">{title}</div> : null}
        <div className="max-h-[80vh] overflow-y-auto p-5">{children}</div>
      </div>
    </div>
  );
};

export default Modal;
