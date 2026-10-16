// Export the web game's chapter data (src/cases/*.js in the blackbird repo) as JSON for Unity, so both games read
// one story. Functions (objective(), combine()) are left behind; Unity has its own versions of those rules.
//   node Tools/cases/export_cases.mjs <blackbird web repo> <blackbird-unity project>
import { writeFileSync, mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const [web, unity] = process.argv.slice(2).map(p => resolve(p));
const out = join(unity, 'Assets', 'BlackBird', 'Resources', 'Cases');
mkdirSync(out, { recursive: true });
const order = ['archer', 'cairo', 'brigid', 'gutman', 'gunsel'];
for (const [i, id] of order.entries()) {
  const m = await import(pathToFileURL(join(web, 'src', 'cases', `${id}.js`)).href);
  const data = { order: i + 1 };
  for (const [k, v] of Object.entries(m)) if (typeof v !== 'function') data[k] = v;
  // the objective rules stay as their source, for reference when porting
  if (m.objective) data.OBJECTIVE_SOURCE = m.objective.toString();
  writeFileSync(join(out, `${id}.json`), JSON.stringify(data, null, 1));
  console.log(id, Object.keys(data).join(' '));
}
