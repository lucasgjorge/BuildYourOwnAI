import type { AskResponse } from '../../shared/api/types'

/** An answer with the file names it cites. */
export function AnswerView({ answer, children }: { answer: AskResponse; children?: React.ReactNode }) {
  return (
    <article className="rounded border p-4">
      {children}
      <p className="whitespace-pre-wrap">{answer.answer}</p>
      {answer.sources.length > 0 && (
        <>
          <h3 className="mt-4 text-sm font-semibold">Fontes</h3>
          <ul className="text-sm text-gray-600">
            {answer.sources.map(s => (
              <li key={`${s.documentId}-${s.chunkIndex}`}>{s.fileName}</li>
            ))}
          </ul>
        </>
      )}
    </article>
  )
}
