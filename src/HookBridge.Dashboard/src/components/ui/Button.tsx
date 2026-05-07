import type { AnchorHTMLAttributes, ButtonHTMLAttributes, JSX } from 'react';
import { Link, type LinkProps } from 'react-router-dom';

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
type ButtonSize = 'sm' | 'md' | 'lg';

type SharedButtonProps = {
  variant?: ButtonVariant;
  size?: ButtonSize;
  className?: string;
};

type NativeButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & SharedButtonProps & {
  to?: never;
  href?: never;
};

type RouterButtonProps = LinkProps & SharedButtonProps & {
  to: string;
  href?: never;
};

type AnchorButtonProps = AnchorHTMLAttributes<HTMLAnchorElement> & SharedButtonProps & {
  href: string;
  to?: never;
};

type ButtonProps = NativeButtonProps | RouterButtonProps | AnchorButtonProps;

const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-primary text-white shadow-lg shadow-primary/20 hover:-translate-y-0.5 hover:bg-primary-dark hover:shadow-xl hover:shadow-primary/25',
  secondary: 'border border-border bg-surface text-text shadow-sm hover:-translate-y-0.5 hover:border-primary-border hover:bg-primary-soft hover:text-primary-dark',
  ghost: 'text-text-muted hover:bg-slate-100 hover:text-text',
  danger: 'bg-error text-white shadow-sm hover:bg-red-700'
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: 'px-3 py-2 text-sm',
  md: 'px-4 py-2.5 text-sm',
  lg: 'px-5 py-3 text-base'
};

const buttonClasses = ({ variant = 'primary', size = 'md', className = '' }: SharedButtonProps): string =>
  `inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition focus-ring disabled:cursor-not-allowed disabled:opacity-60 ${variantClasses[variant]} ${sizeClasses[size]} ${className}`;

const Button = (props: ButtonProps): JSX.Element => {
  const { variant = 'primary', size = 'md', className = '' } = props;
  const classes = buttonClasses({ variant, size, className });

  if ('to' in props && props.to) {
    const { variant: _variant, size: _size, className: _className, to, ...linkProps } = props;
    return <Link to={to} className={classes} {...linkProps} />;
  }

  if ('href' in props && props.href) {
    const { variant: _variant, size: _size, className: _className, href, ...anchorProps } = props;
    return <a href={href} className={classes} {...anchorProps} />;
  }

  const nativeProps = props as NativeButtonProps;
  const { variant: _variant, size: _size, className: _className, type, ...buttonProps } = nativeProps;
  const buttonType = type ?? 'button';
  return <button type={buttonType} className={classes} {...buttonProps} />;
};

export default Button;
