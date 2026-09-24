import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp } from '../../test/render'
import { anonymous, loggedIn, server } from '../../test/server'

describe('home', () => {
  // C30
  it('home explains how it works in three steps', async () => {
    server.use(anonymous())

    renderApp('/')

    expect(await screen.findByRole('heading', { level: 1, name: /Uma IA para cada assunto/ })).toBeInTheDocument()
    const how = screen.getByRole('region', { name: 'Como funciona' })
    const steps = within(how).getAllByRole('heading', { level: 3 }).map(h => h.textContent)
    expect(steps).toEqual(['Monte a organização', 'Crie as IAs', 'Pergunte ao Jev'])
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/$/))
  })

  // C31
  it('home shows what you can build', async () => {
    server.use(anonymous())

    renderApp('/')

    const build = await screen.findByRole('region', { name: 'O que dá para montar' })
    const examples = within(build).getAllByRole('heading', { level: 3 })
    expect(examples.length).toBeGreaterThanOrEqual(3)
    for (const example of examples) {
      const assistants = within(build).getByRole('list', { name: `IAs de ${example.textContent}` })
      expect(within(assistants).getAllByRole('listitem').length).toBeGreaterThan(0)
    }
    expect(screen.getByRole('region', { name: 'O que ninguém sabe vira Lacuna' })).toHaveTextContent('Lacunas')
  })

  // C32
  it('home without session offers sign up and sign in', async () => {
    server.use(anonymous())

    renderApp('/')

    const account = await screen.findByRole('navigation', { name: 'Conta' })
    expect(within(account).getByRole('link', { name: 'Criar conta' })).toHaveAttribute('href', '/register')
    expect(within(account).getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/login')
    expect(screen.queryByRole('link', { name: 'Abrir o chat' })).not.toBeInTheDocument()
  })

  // C33
  it('home with session opens the chat', async () => {
    server.use(loggedIn())

    renderApp('/')

    const account = await screen.findByRole('navigation', { name: 'Conta' })
    expect(await within(account).findByRole('link', { name: 'Abrir o chat' })).toHaveAttribute('href', '/organizations')
    expect(screen.queryByRole('link', { name: 'Criar conta' })).not.toBeInTheDocument()
  })
})
