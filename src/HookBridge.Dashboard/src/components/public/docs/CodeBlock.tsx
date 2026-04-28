import { useMemo, useState } from 'react';

type CodeLanguage = 'curl' | 'json' | 'http' | 'bash' | 'text';

interface CodeBlockProps {
  code: string;
  language?: CodeLanguage;
  title?: string;
}

const languageClassMap: Record<CodeLanguage, string> = {
  curl: 'text-emerald-300',
  json: 'text-sky-300',
  http: 'text-amber-300',
  bash: 'text-fuchsia-300',
  text: 'text-slate-300'
};

const CodeBlock = ({ code, language = 'text', title }: CodeBlockProps): JSX.Element => {
  const [copied, setCopied] = useState(false);

  const languageLabel = useMemo(() => language.toUpperCase(), [language]);

  const handleCopy = async (): Promise<void> => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="overflow-hidden rounded-xl border border-slate-700 bg-slate-950 shadow-sm">
      <div className="flex items-center justify-between border-b border-slate-800 px-4 py-2">
        <div className="flex items-center gap-3">
          <span className={`text-xs font-semibold tracking-wide ${languageClassMap[language]}`}>{languageLabel}</span>
          {title ? <span className="text-xs text-slate-400">{title}</span> : null}
        </div>
        <button
          type="button"
          onClick={() => void handleCopy()}
          className="rounded-md border border-slate-700 px-2 py-1 text-xs font-medium text-slate-200 transition hover:border-slate-500 hover:text-white"
        >
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      <pre className="overflow-x-auto p-4 text-sm leading-6 text-slate-100">
        <code>{code}</code>
      </pre>
    </div>
  );
};

export default CodeBlock;
