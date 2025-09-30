#!/usr/bin/env node

const fs = require('fs')
const path = require('path')

// Read environment variables
const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://85.211.170.104/api'

// Create the script to inject environment variables
const envScript = `
// Injected environment variables
window.__NEXT_PUBLIC_API_URL__ = '${apiUrl}';
`

// Write to public directory
const publicDir = path.join(__dirname, '../public')
if (!fs.existsSync(publicDir)) {
  fs.mkdirSync(publicDir, { recursive: true })
}

fs.writeFileSync(path.join(publicDir, 'env.js'), envScript)
console.log('✅ Environment variables injected:', { apiUrl })
