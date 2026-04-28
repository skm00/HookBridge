import type { JSX, ReactNode } from 'react';

type PageHeaderProps = {
  title: string;
  description?: string;
  actions?: ReactNode;
};

const PageHeader = ({ title, description, actions }: PageHeaderProps): JSX.Element => {
  return (
    <header className="flex flex-col gap-3 border-b border-border pb-3 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 className="text-2xl font-semibold text-text">{title}</h1>
        {description ? <p className="mt-1 text-sm text-text-muted">{description}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </header>
  );
};

export default PageHeader;
