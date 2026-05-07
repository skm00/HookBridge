import type { HTMLAttributes, JSX } from 'react';

const Card = ({ className = '', ...props }: HTMLAttributes<HTMLDivElement>): JSX.Element => {
  return <div className={`rounded-2xl border border-border/90 bg-surface/95 p-5 shadow-soft backdrop-blur ${className}`} {...props} />;
};

export default Card;
