import type { JSX, ReactNode } from 'react';
import Badge from './Badge';

type SectionHeaderProps = {
  eyebrow?: string;
  title: string;
  description?: string;
  align?: 'left' | 'center';
  action?: ReactNode;
};

const SectionHeader = ({ eyebrow, title, description, align = 'center', action }: SectionHeaderProps): JSX.Element => {
  const isCenter = align === 'center';

  return (
    <div className={`${isCenter ? 'mx-auto max-w-3xl text-center' : 'max-w-3xl'} ${action ? 'sm:max-w-none' : ''}`}>
      <div className={action ? 'items-end justify-between gap-6 sm:flex' : undefined}>
        <div className={isCenter && !action ? 'mx-auto max-w-3xl' : 'max-w-3xl'}>
          {eyebrow ? <Badge tone="brand" className="border-primary-border bg-primary-soft text-primary-dark">{eyebrow}</Badge> : null}
          <h2 className="mt-4 text-3xl font-bold tracking-tight text-text sm:text-4xl">{title}</h2>
          {description ? <p className="mt-4 text-base leading-7 text-text-muted sm:text-lg">{description}</p> : null}
        </div>
        {action ? <div className="mt-6 shrink-0 sm:mt-0">{action}</div> : null}
      </div>
    </div>
  );
};

export default SectionHeader;
