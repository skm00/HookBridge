import type { HTMLAttributes, JSX } from 'react';

type PageContainerProps = HTMLAttributes<HTMLDivElement> & {
  size?: 'md' | 'lg' | 'xl' | 'full';
};

const sizeClasses: Record<NonNullable<PageContainerProps['size']>, string> = {
  md: 'max-w-4xl',
  lg: 'max-w-6xl',
  xl: 'max-w-7xl',
  full: 'max-w-[1800px]'
};

const PageContainer = ({ size = 'xl', className = '', ...props }: PageContainerProps): JSX.Element => {
  return <div className={`mx-auto w-full ${sizeClasses[size]} px-4 sm:px-6 lg:px-8 ${className}`} {...props} />;
};

export default PageContainer;
