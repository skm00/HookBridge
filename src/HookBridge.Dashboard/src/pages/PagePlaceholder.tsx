type PagePlaceholderProps = {
  title: string;
  description: string;
};

const PagePlaceholder = ({ title, description }: PagePlaceholderProps): JSX.Element => {
  return (
    <section className="space-y-4">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900">{title}</h2>
        <p className="text-sm text-slate-600">{description}</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <h3 className="text-sm font-semibold text-slate-700">Card 1</h3>
          <p className="mt-2 text-sm text-slate-500">Dashboard widgets will be implemented here.</p>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <h3 className="text-sm font-semibold text-slate-700">Card 2</h3>
          <p className="mt-2 text-sm text-slate-500">Live metrics and charts can be added later.</p>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <h3 className="text-sm font-semibold text-slate-700">Card 3</h3>
          <p className="mt-2 text-sm text-slate-500">Actions and tables can be added in future tasks.</p>
        </div>
      </div>
    </section>
  );
};

export default PagePlaceholder;
