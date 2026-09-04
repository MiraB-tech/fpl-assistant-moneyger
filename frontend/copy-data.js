// Copies data/*.json from the project's shared data/ folder into
// frontend/public/data, so the React app can fetch it as plain static
// files. Runs automatically before `npm run dev` and `npm run build`
// (see the "predev"/"prebuild" scripts in package.json) — you should
// never need to run this by hand.

import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SOURCE_DIR = path.join(__dirname, '..', 'data')
const DEST_DIR = path.join(__dirname, 'public', 'data')

fs.mkdirSync(DEST_DIR, { recursive: true })

const files = fs.readdirSync(SOURCE_DIR).filter((f) => f.endsWith('.json'))
for (const file of files) {
  fs.copyFileSync(path.join(SOURCE_DIR, file), path.join(DEST_DIR, file))
}

console.log(`Copied ${files.length} data file(s) to frontend/public/data`)
