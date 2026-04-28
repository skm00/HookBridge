type FieldErrorProps = {
  errors?: string[];
};

const FieldError = ({ errors }: FieldErrorProps): JSX.Element | null => {
  if (!errors || errors.length === 0) {
    return null;
  }

  return (
    <ul className="mt-1 list-disc space-y-1 pl-5 text-xs text-red-600">
      {errors.map((error) => (
        <li key={error}>{error}</li>
      ))}
    </ul>
  );
};

export default FieldError;
