import type { JSX } from 'react';

type SkeletonTableProps = {
  rows?: number;
  columns?: number;
};

const SkeletonTable = ({ rows = 6, columns = 6 }: SkeletonTableProps): JSX.Element => {
  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white" aria-hidden="true">
      <div className="min-w-full animate-pulse p-4">
        <div className="mb-4 grid gap-3" style={{ gridTemplateColumns: `repeat(${columns}, minmax(110px, 1fr))` }}>
          {Array.from({ length: columns }, (_, index) => (
            <div key={`header-${index}`} className="h-3 rounded bg-slate-200" />
          ))}
        </div>

        <div className="space-y-3">
          {Array.from({ length: rows }, (_, rowIndex) => (
            <div key={`row-${rowIndex}`} className="grid gap-3" style={{ gridTemplateColumns: `repeat(${columns}, minmax(110px, 1fr))` }}>
              {Array.from({ length: columns }, (_, colIndex) => (
                <div key={`cell-${rowIndex}-${colIndex}`} className="h-4 rounded bg-slate-100" />
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default SkeletonTable;
