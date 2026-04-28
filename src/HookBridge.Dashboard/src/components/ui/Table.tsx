import type { HTMLAttributes, JSX } from 'react';

const TableContainer = ({ className = '', ...props }: HTMLAttributes<HTMLDivElement>): JSX.Element => (
  <div className={`overflow-x-auto rounded-xl border border-border bg-surface shadow-sm ${className}`} {...props} />
);

export default TableContainer;
