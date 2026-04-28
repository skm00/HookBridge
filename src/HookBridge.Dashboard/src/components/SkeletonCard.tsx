import type { JSX } from 'react';

const SkeletonCard = (): JSX.Element => {
  return (
    <div className="animate-pulse rounded-xl border border-slate-200 bg-white p-5 shadow-sm" aria-hidden="true">
      <div className="h-4 w-24 rounded bg-slate-200" />
      <div className="mt-3 h-7 w-32 rounded bg-slate-200" />
      <div className="mt-2 h-3 w-20 rounded bg-slate-100" />
    </div>
  );
};

export default SkeletonCard;
