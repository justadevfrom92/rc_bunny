import { createRoot } from 'react-dom/client';
import PlanFilter from './PlanFilter';

// Razor renders the whole page. An element like <div data-island="PlanFilter" data-props='{...}'> becomes interactive here.
const islands = { PlanFilter };

document.querySelectorAll<HTMLElement>('[data-island]').forEach((el) => {
  const Island = islands[el.dataset.island as keyof typeof islands];
  if (Island) createRoot(el).render(<Island {...JSON.parse(el.dataset.props ?? '{}')} />);
});
