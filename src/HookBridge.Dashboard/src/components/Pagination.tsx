type PaginationProps = {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
  onPageChange: (nextPage: number) => void;
  onPageSizeChange: (nextPageSize: number) => void;
};

const pageSizes = [10, 25, 50, 100] as const;

export const Pagination = ({
  pageNumber,
  pageSize,
  totalCount,
  totalPages,
  hasPreviousPage,
  hasNextPage,
  onPageChange,
  onPageSizeChange
}: PaginationProps): JSX.Element => {
  const safeTotalPages = Math.max(totalPages, 1);
  const startRecord = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endRecord = totalCount === 0 ? 0 : Math.min(pageNumber * pageSize, totalCount);

  return (
    <div className="flex flex-col gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-700 sm:flex-row sm:items-center sm:justify-between">
      <div>Showing {startRecord}–{endRecord} of {totalCount} records</div>

      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-2">
          <span>Page size</span>
          <select
            value={pageSize}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
            className="rounded-md border border-slate-300 bg-white px-2 py-1"
          >
            {pageSizes.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>

        <button
          type="button"
          disabled={!hasPreviousPage}
          onClick={() => onPageChange(pageNumber - 1)}
          className="rounded-md border border-slate-300 bg-white px-3 py-1.5 font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
        >
          Previous
        </button>

        <span>
          Page {Math.min(pageNumber, safeTotalPages)} / {safeTotalPages}
        </span>

        <button
          type="button"
          disabled={!hasNextPage}
          onClick={() => onPageChange(pageNumber + 1)}
          className="rounded-md border border-slate-300 bg-white px-3 py-1.5 font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
        >
          Next
        </button>
      </div>
    </div>
  );
};
