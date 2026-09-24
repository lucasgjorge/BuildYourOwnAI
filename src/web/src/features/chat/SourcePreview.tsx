import { useEffect, useRef } from 'react'
import { errorTitle } from '../../shared/api/client'
import { Alert } from '../../shared/ui'
import { useDocumentChunk } from '../organizations/api'
import type { SourceRef } from './Thread'

/**
 * The chunk an answer cited, highlighted between its neighbors. A column beside the chat on wide screens,
 * a sheet over it on narrow ones.
 */
export function SourcePreview({ organizationId, source, onClose }: { organizationId: string; source: SourceRef; onClose: () => void }) {
  const chunk = useDocumentChunk(organizationId, source.documentId, source.chunkIndex)
  const panel = useRef<HTMLDivElement>(null)
  const cited = useRef<HTMLParagraphElement>(null)

  // Centers the cited chunk by scrolling the panel only: scrollIntoView would also move the page sideways (AC 17).
  useEffect(() => {
    if (panel.current && cited.current)
      panel.current.scrollTop = cited.current.offsetTop - panel.current.clientHeight / 2
  }, [chunk.data])

  return (
    <aside
      aria-label="Prévia do trecho"
      className="fixed inset-0 z-20 flex flex-col bg-surface lg:sticky lg:inset-auto lg:top-0 lg:z-auto lg:h-screen lg:w-[28rem] lg:shrink-0 lg:border-l lg:border-line"
    >
      <header className="flex items-start justify-between gap-4 border-b border-line px-5 py-4">
        <div className="min-w-0">
          <p className="font-mono text-[11px] tracking-wider text-muted uppercase">Prévia do trecho</p>
          {chunk.data && (
            <>
              <h2 className="mt-1 truncate font-mono text-sm font-medium">{chunk.data.fileName}</h2>
              <p className="text-xs text-muted">Trecho {source.chunkIndex + 1} de {chunk.data.chunkCount}</p>
            </>
          )}
        </div>
        <button onClick={onClose} className="shrink-0 rounded-md border border-line px-2.5 py-1 text-sm hover:border-ink/40">
          Fechar prévia
        </button>
      </header>

      <div ref={panel} className="relative flex-1 overflow-y-auto px-5 py-5">
        {chunk.isPending && <p className="text-sm text-muted">Carregando trecho…</p>}
        {chunk.isError && <Alert>{errorTitle(chunk.error)}</Alert>}
        {chunk.data && (
          <ol className="flex flex-col gap-3 text-sm leading-relaxed">
            {chunk.data.chunks.map(c => {
              const isCited = c.index === source.chunkIndex
              return (
                <li key={c.index}>
                  <p
                    ref={isCited ? cited : undefined}
                    aria-current={isCited ? 'true' : undefined}
                    className={isCited
                      ? 'rounded-md border-l-4 border-route bg-route-soft px-3 py-2 text-ink'
                      : 'px-3 text-muted'}
                  >
                    {c.content}
                  </p>
                </li>
              )
            })}
          </ol>
        )}
      </div>
    </aside>
  )
}
