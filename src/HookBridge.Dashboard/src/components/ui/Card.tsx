import type { HTMLAttributes, JSX } from 'react';

const Card = ({ className = '', ...props }: HTMLAttributes<HTMLDivElement>): JSX.Element => {
  return <div className={`rounded-xl border border-border bg-surface p-5 shadow-sm ${className}`} {...props} />;
};

export default Card;
