import { createRoot } from 'react-dom/client';
import type { ComponentType } from 'react';

// Each island's code is its own chunk, loaded only on pages that use it.
const islands: Record<string, () => Promise<{ default: ComponentType<any> }>> = {
  ZipCheck: () => import('./islands/ZipCheck'),
  PlanBrowser: () => import('./islands/PlanBrowser'),
  ByopBuilder: () => import('./islands/ByopBuilder'),
  ContactForm: () => import('./islands/ContactForm'),
  UsageChart: () => import('./islands/UsageChart'),
};

export function mountAll(elements: HTMLElement[]) {
  for (const el of elements) {
    const name = el.dataset.island!;
    const load = islands[name];
    if (!load) continue;
    const script = el.nextElementSibling;
    const props = script?.matches('script[data-island-props]') ? JSON.parse(script.textContent || '{}') : {};
    load()
      .then(({ default: Island }) => createRoot(el).render(<Island {...props} />))
      .catch((err) => console.warn(`Island ${name} failed to load; the server-rendered version stays.`, err));
  }
}
