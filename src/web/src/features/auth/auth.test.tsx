import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { anonymous, server } from '../../test/server'
import { renderApp } from '../../test/render'

describe('session guard', () => {
  // C29
  it.each(['/assistants', '/assistants/abc'])('redirects to login without session (%s)', async path => {
    server.use(anonymous())

    renderApp(path)

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login'))
    expect(screen.getByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
  })

  // C29
  it('redirects to login without session, but /register opens directly', async () => {
    server.use(anonymous())

    renderApp('/register')

    expect(await screen.findByRole('heading', { name: 'Criar conta' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/register')
  })
})
