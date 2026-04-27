import type { PagedRequest } from '../types/pagination';

type SortableHeaderProps = {
  label: string;
  sortKey: string;
  currentSortBy?: string;
  currentSortDirection?: PagedRequest['sortDirection'];
  onSort: (sortBy: string, sortDirection: NonNullable<PagedRequest['sortDirection']>) => void;
};

export const SortableHeader = ({
  label,
  sortKey,
  currentSortBy,
  currentSortDirection,
  onSort
}: SortableHeaderProps): JSX.Element => {
  const isActive = currentSortBy === sortKey;
  const nextDirection: NonNullable<PagedRequest['sortDirection']> = isActive && currentSortDirection === 'asc' ? 'desc' : 'asc';
  const indicator = isActive ? (currentSortDirection === 'asc' ? '▲' : '▼') : '↕';

  return (
    <button
      type="button"
      onClick={() => onSort(sortKey, nextDirection)}
      className="inline-flex items-center gap-1 font-semibold uppercase tracking-wide text-slate-600 hover:text-slate-900"
      title={`Sort by ${label}`}
    >
      <span>{label}</span>
      <span aria-hidden="true">{indicator}</span>
    </button>
  );
};
