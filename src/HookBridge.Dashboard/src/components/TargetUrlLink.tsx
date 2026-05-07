import type { JSX } from 'react';

type TargetUrlLinkProps = {
  url: string | null | undefined;
  displayText?: string;
  className?: string;
};

const isClickableHttpUrl = (value: string): boolean => {
  try {
    const parsedUrl = new URL(value);
    return parsedUrl.protocol === 'http:' || parsedUrl.protocol === 'https:';
  } catch {
    return false;
  }
};

export const TargetUrlLink = ({ url, displayText, className = '' }: TargetUrlLinkProps): JSX.Element => {
  const trimmedUrl = url?.trim();

  if (!trimmedUrl) {
    return <>{'-'}</>;
  }

  if (!isClickableHttpUrl(trimmedUrl)) {
    return <span className={className}>{displayText ?? trimmedUrl}</span>;
  }

  return (
    <a
      href={trimmedUrl}
      target="_blank"
      rel="noopener noreferrer"
      title={trimmedUrl}
      className={`text-brand-700 underline decoration-brand-300 underline-offset-2 hover:text-brand-800 hover:decoration-brand-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-500 ${className}`}
    >
      {displayText ?? trimmedUrl}
    </a>
  );
};
