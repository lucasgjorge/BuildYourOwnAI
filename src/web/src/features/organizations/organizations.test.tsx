import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { renderApp } from '../../test/render'
import {
  deferred,
  loggedIn,
  organization,
  organizationPageHandlers,
  organizationSummary,
  problem,
  server,
} from '../../test/server'

const BASE = '/organizations/o1/knowledge'

describe('organizations entry', () => {
  // rag-mvp C33 (create) - now on the empty /organizations page
  it('shows problem title and keeps input (create organization)', async () => {
    server.use(loggedIn(), http.post('*/api/organizations', () => problem(400, 'Nome inválido')))
    const { user } = renderApp('/organizations')

    const main = await screen.findByRole('main')
    await user.type(within(main).getByLabelText('Nome da organização'), 'ACME')
    await user.click(within(main).getByRole('button', { name: 'Criar organização' }))

    expect(await within(main).findByRole('alert')).toHaveTextContent('Nome inválido')
    expect(within(main).getByLabelText('Nome da organização')).toHaveValue('ACME')
  })

  // rag-mvp C51
  it('create button shows processing', async () => {
    const { gate, release } = deferred()
    server.use(loggedIn(), http.post('*/api/organizations', async () => {
      await gate
      return problem(400, 'Nome inválido')
    }))
    const { user } = renderApp('/organizations')

    const main = await screen.findByRole('main')
    await user.type(within(main).getByLabelText('Nome da organização'), 'ACME')
    await user.click(within(main).getByRole('button', { name: 'Criar organização' }))

    expect(await within(main).findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await within(main).findByRole('button', { name: 'Criar organização' })).toBeEnabled()
  })

  // rag-mvp C47 - the list is loading, then the first organization opens
  it('organizations list shows loading', async () => {
    const { gate, release } = deferred()
    server.use(
      ...organizationPageHandlers(),
      http.get('*/api/organizations', async () => {
        await gate
        return HttpResponse.json([organizationSummary])
      }),
    )

    renderApp('/organizations')

    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
    expect(screen.getAllByText('Carregando...').length).toBeGreaterThan(0)
    release()
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/organizations/o1'))
  })
})

describe('organization base tab', () => {
  // C57 (rag-mvp C31) -> org-chat C25
  it('organization page lists documents and assistants', async () => {
    const created: unknown[] = []
    server.use(
      ...organizationPageHandlers(),
      http.post('*/api/assistants', async ({ request }) => {
        created.push(await request.json())
        return HttpResponse.json({ id: 'a3' }, { status: 201 })
      }),
    )
    const { user } = renderApp(BASE)

    expect(await screen.findByText('politicas.pdf')).toBeInTheDocument()
    expect(screen.getByText('manual.txt')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Direto' })).toHaveAttribute('href', '/assistants/a1')
    expect(screen.getByRole('link', { name: 'Professor' })).toBeInTheDocument()
    // Each AI shows its "Quando usar", or that it stays out of the automatic choice without one.
    expect(screen.getByRole('link', { name: 'Direto' }).closest('li')).toHaveTextContent('respostas curtas')
    expect(screen.getByRole('link', { name: 'Professor' }).closest('li')).toHaveTextContent('Fora da escolha automática')
    expect(screen.getByLabelText(/Arquivo/)).toHaveAttribute('type', 'file')
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeInTheDocument()

    await user.type(screen.getByLabelText('Nome'), 'Resumo')
    await user.type(screen.getByLabelText('Quando usar esta IA'), 'quando a pessoa quer um resumo')
    await user.click(screen.getByRole('button', { name: 'Criar IA' }))

    await waitFor(() => expect(created).toEqual([
      { organizationId: 'o1', name: 'Resumo', instructions: null, routingDescription: 'quando a pessoa quer um resumo' },
    ]))
  })

  // C59 -> org-chat C25
  it('organization page empty, loading and not found', async () => {
    const { gate, release } = deferred()
    server.use(
      loggedIn(),
      http.get('*/api/organizations/o1', async () => {
        await gate
        return HttpResponse.json({ ...organization, assistants: [] })
      }),
      http.get('*/api/organizations/o1/documents', () => HttpResponse.json([])),
      http.get('*/api/organizations/o9', () => problem(404, 'Organização não encontrada.')),
    )

    const first = renderApp(BASE)
    // Once the sidebar settled, the only loader left is the organization's own.
    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getAllByText('Carregando...')).toHaveLength(1))
    release()
    expect(await screen.findByText('Nenhuma IA nesta organização')).toBeInTheDocument()
    first.unmount()

    renderApp('/organizations/o9/knowledge')
    expect(await screen.findByRole('alert')).toHaveTextContent('Organização não encontrada.')
  })

  // rag-mvp C34 (document) -> org-chat C25
  it('delete asks for confirmation (document)', async () => {
    const deletes: string[] = []
    server.use(
      ...organizationPageHandlers(),
      http.delete('*/api/organizations/o1/documents/:documentId', ({ params }) => {
        deletes.push(String(params.documentId))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp(BASE)
    const button = await screen.findByRole('button', { name: 'Apagar documento manual.txt' })

    await user.click(button)
    expect(deletes).toEqual([])
    await user.click(button)
    await waitFor(() => expect(deletes).toEqual(['d1']))
  })

  // rag-mvp C34 (assistant) -> org-chat C25
  it('delete asks for confirmation (assistant)', async () => {
    const deletes: string[] = []
    server.use(
      ...organizationPageHandlers(),
      http.delete('*/api/assistants/:id', ({ params }) => {
        deletes.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp(BASE)
    const button = await screen.findByRole('button', { name: 'Apagar IA Direto' })

    await user.click(button)
    expect(deletes).toEqual([])
    await user.click(button)
    await waitFor(() => expect(deletes).toEqual(['a1']))
  })

  // C58 -> org-chat C26
  it('organization delete confirms', async () => {
    const deletes: string[] = []
    server.use(
      ...organizationPageHandlers(),
      http.delete('*/api/organizations/:id', ({ params }) => {
        deletes.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp(BASE)
    const button = await screen.findByRole('button', { name: 'Apagar organização ACME' })

    await user.click(button)
    expect(confirm).toHaveBeenCalledTimes(1)
    expect(confirm.mock.calls[0][0]).toMatch(/IAs, os documentos e as lacunas/)
    expect(deletes).toEqual([])

    await user.click(button)
    await waitFor(() => expect(deletes).toEqual(['o1']))
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/organizations$/))
  })

  // rag-mvp C35 (upload) -> org-chat C25
  it('disables button while processing (upload)', async () => {
    const { gate, release } = deferred()
    server.use(
      ...organizationPageHandlers(),
      http.post('*/api/organizations/o1/documents', async () => {
        await gate
        return HttpResponse.json({ id: 'd3', fileName: 'novo.txt', sizeBytes: 5, chunkCount: 1, uploadedAt: '2026-09-23T11:00:00Z' }, { status: 201 })
      }),
    )
    const { user } = renderApp(BASE)

    await user.upload(await screen.findByLabelText(/Arquivo/), new File(['texto'], 'novo.txt', { type: 'text/plain' }))
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await screen.findByRole('button', { name: 'Enviar' })).toBeEnabled()
  })

  // rag-mvp C48 -> org-chat C25
  it('empty document list shows only upload', async () => {
    server.use(
      loggedIn(),
      http.get('*/api/organizations/o1', () => HttpResponse.json(organization)),
      http.get('*/api/organizations/o1/documents', () => HttpResponse.json([])),
    )

    renderApp(BASE)

    const upload = await screen.findByLabelText(/Arquivo/)
    const section = upload.closest('section')!
    await waitFor(() => expect(within(section).queryByText('Carregando...')).not.toBeInTheDocument())
    expect(within(section).queryAllByRole('listitem')).toHaveLength(0)
    expect(within(section).getByRole('button', { name: 'Enviar' })).toBeInTheDocument()
  })

  // rag-mvp C49 -> org-chat C25
  it('upload error shows problem title', async () => {
    server.use(
      ...organizationPageHandlers(),
      http.post('*/api/organizations/o1/documents', () => problem(415, 'Formato não suportado. Use .pdf, .txt, .md.')),
    )
    const { user } = renderApp(BASE)

    await user.upload(await screen.findByLabelText(/Arquivo/), new File(['x'], 'nota.txt', { type: 'text/plain' }))
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Formato não suportado. Use .pdf, .txt, .md.')
  })
})

describe('session guard (rag-mvp C50)', () => {
  it('session guard states: loading while the session is unknown', async () => {
    const { gate, release } = deferred()
    server.use(http.get('*/api/auth/manage/info', async () => {
      await gate
      return HttpResponse.json({ email: 'ana@test.local', isEmailConfirmed: false })
    }))

    renderApp('/organizations')

    expect(await screen.findByText('Carregando...')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Organizações' })).not.toBeInTheDocument()
    release()
    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
  })

  it('session guard states: a non-401 failure shows the problem title and stays', async () => {
    server.use(http.get('*/api/auth/manage/info', () => problem(500, 'Servidor indisponível')))

    renderApp('/organizations')

    expect(await screen.findByRole('alert')).toHaveTextContent('Servidor indisponível')
    expect(screen.getByTestId('location')).toHaveTextContent('/organizations')
  })
})
