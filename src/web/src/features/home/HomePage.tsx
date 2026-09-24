import { Link } from 'react-router'
import { laneColor } from '../../shared/lanes'
import { useSession } from '../auth/api'

const steps = [
  {
    title: 'Monte a organização',
    text: 'Suba PDFs, TXT ou Markdown que você já tem: manuais, políticas, apostilas. Todas as IAs da organização respondem a partir deles.',
  },
  {
    title: 'Crie as IAs',
    text: 'Dê a cada uma um nome, um jeito de responder e o “Quando usar”. É assim que o chat sabe quando chamar cada uma.',
  },
  {
    title: 'Pergunte no chat',
    text: 'Escreva no chat. A pergunta vai sozinha para a IA certa, a resposta mostra o trecho do documento, e você pede a opinião de outra IA com um clique.',
  },
]

const examples = [
  { name: 'RH e cultura da empresa', assistants: ['RH', 'Culture', 'Onboarding'], from: 'políticas internas, código de conduta, manual do colaborador' },
  { name: 'Time de engenharia', assistants: ['Tech Team', 'Arquitetura', 'Plantão'], from: 'runbooks, ADRs, guias de estilo' },
  { name: 'Estudo para uma prova', assistants: ['Professor', 'Resumo', 'Simulado'], from: 'apostilas e anotações do curso' },
  { name: 'Atendimento de uma loja', assistants: ['Produto', 'Trocas', 'Entregas'], from: 'catálogo, política de trocas, prazos de entrega' },
]

/** `/`: what the product does and what you can build with it. Public. */
export function HomePage() {
  const session = useSession()
  const signedIn = session.isSuccess

  const actions = signedIn ? (
    <Link to="/organizations" className="rounded-md bg-ink px-4 py-2 text-sm font-medium text-surface hover:bg-ink/90">Abrir o chat</Link>
  ) : (
    <>
      <Link to="/login" className="rounded-md px-4 py-2 text-sm font-medium hover:bg-surface">Entrar</Link>
      <Link to="/register" className="rounded-md bg-ink px-4 py-2 text-sm font-medium text-surface hover:bg-ink/90">Criar conta</Link>
    </>
  )

  return (
    <div className="min-h-screen">
      <header className="mx-auto flex max-w-6xl items-center justify-between px-6 py-5">
        <span className="font-display text-lg font-bold tracking-tight">BuildYourOwnAI</span>
        <nav aria-label="Conta" className="flex items-center gap-2">{actions}</nav>
      </header>

      <main>
        <section className="mx-auto grid max-w-6xl items-center gap-12 px-6 pt-10 pb-20 lg:grid-cols-[1.1fr_1fr]">
          <div>
            <h1 className="font-display text-4xl leading-[1.05] font-bold tracking-tight md:text-6xl">
              Uma IA para cada assunto. O chat sabe para qual perguntar.
            </h1>
            <p className="mt-6 max-w-xl text-lg text-muted">
              Suba os documentos que você já tem, crie IAs com jeitos diferentes de responder e converse num chat só.
              Cada resposta diz quem respondeu e de qual documento veio.
            </p>
            <div className="mt-8 flex gap-2">{actions}</div>
          </div>
          <Switchboard />
        </section>

        <section aria-labelledby="how" className="border-y border-line bg-surface">
          <div className="mx-auto max-w-6xl px-6 py-16">
            <h2 id="how" className="font-display text-3xl font-bold tracking-tight">Como funciona</h2>
            <ol className="mt-10 grid gap-10 md:grid-cols-3">
              {steps.map((step, i) => (
                <li key={step.title} className="border-t-2 border-ink pt-4">
                  <span className="font-mono text-xs text-muted">passo {i + 1} de 3</span>
                  <h3 className="mt-1 text-lg font-semibold">{step.title}</h3>
                  <p className="mt-2 text-muted">{step.text}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section aria-labelledby="build" className="mx-auto max-w-6xl px-6 py-16">
          <h2 id="build" className="font-display text-3xl font-bold tracking-tight">O que dá para montar</h2>
          <p className="mt-2 max-w-2xl text-muted">Uma organização por contexto, com uma IA por jeito de responder. Alguns exemplos:</p>
          <ul className="mt-10 grid gap-4 sm:grid-cols-2">
            {examples.map(example => (
              <li key={example.name} className="rounded-xl border border-line bg-surface p-5">
                <h3 className="font-semibold">{example.name}</h3>
                <p className="mt-1 text-sm text-muted">A partir de {example.from}.</p>
                <ul aria-label={`IAs de ${example.name}`} className="mt-4 flex flex-wrap gap-2">
                  {example.assistants.map((a, i) => (
                    <li key={a} className="inline-flex items-center gap-2 rounded-full border border-line px-3 py-1 text-sm">
                      <span aria-hidden className="h-2 w-2 rounded-full" style={{ background: laneColor(i) }} />
                      {a}
                    </li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </section>

        <section aria-labelledby="gaps" className="mx-auto max-w-6xl px-6 pb-20">
          <div className="grid gap-6 rounded-2xl bg-ink p-8 text-surface md:grid-cols-[1fr_1.4fr] md:p-12">
            <h2 id="gaps" className="font-display text-3xl font-bold tracking-tight">O que ninguém sabe vira Lacuna</h2>
            <p className="text-surface/80">
              Quando nenhuma IA encontra a resposta nos documentos, a pergunta vai para Lacunas, com quantas vezes foi feita.
              Você responde uma vez e a resposta passa a ser conhecimento da organização: da próxima vez, a IA cita a sua resposta.
            </p>
          </div>
        </section>
      </main>

      <footer className="border-t border-line">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-6 py-8">
          <span className="text-sm text-muted">Pronto para montar a sua?</span>
          <div className="flex gap-2">{actions}</div>
        </div>
      </footer>
    </div>
  )
}

/** The signature: one question, the automatic choice in the middle, and the lane that answers lit up. */
function Switchboard() {
  const lanes = ['RH', 'Culture', 'Tech Team']
  return (
    <figure aria-label="Exemplo: a pergunta vai sozinha para a IA de RH" className="rounded-2xl border border-line bg-surface p-6">
      <p className="ml-auto w-fit rounded-2xl rounded-br-sm bg-ink px-4 py-2.5 text-surface">Como peço férias?</p>
      <svg viewBox="0 0 320 150" className="my-4 w-full" aria-hidden>
        <circle cx="40" cy="75" r="18" fill="var(--color-route)" />
        <text x="40" y="79" textAnchor="middle" fontSize="11" fontFamily="var(--font-mono)" fill="#fff">auto</text>
        {lanes.map((lane, i) => {
          const y = 25 + i * 50
          const lit = i === 0
          return (
            <g key={lane}>
              <path
                d={`M58 75 C 140 75, 150 ${y}, 230 ${y}`}
                pathLength={1}
                fill="none"
                stroke={lit ? laneColor(i) : 'var(--color-line)'}
                strokeWidth={lit ? 3 : 1.5}
                className={lit ? 'route-path' : undefined}
              />
              <circle cx="238" cy={y} r="5" fill={laneColor(i)} opacity={lit ? 1 : 0.35} />
              <text x="250" y={y + 4} fontSize="13" fontFamily="var(--font-sans)" fill="var(--color-ink)" opacity={lit ? 1 : 0.45}>{lane}</text>
            </g>
          )
        })}
      </svg>
      <figcaption className="rounded-xl border border-line p-4" style={{ borderLeftColor: laneColor(0), borderLeftWidth: 3 }}>
        <span className="text-sm font-semibold" style={{ color: laneColor(0) }}>Respondido por RH</span>
        <span className="ml-2 rounded bg-route-soft px-1.5 py-0.5 font-mono text-[11px] text-route">escolha automática</span>
        <p className="mt-2 text-sm">Pelo portal do colaborador, com 30 dias de antecedência e aprovação do gestor.</p>
        <p className="mt-2 font-mono text-xs text-muted">01_rh.txt</p>
      </figcaption>
    </figure>
  )
}
