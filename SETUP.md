# Development Setup Guide

## Required Environment Variables

1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```

2. Get Stripe Test Keys:
   - Go to [Stripe Dashboard](https://dashboard.stripe.com/test/apikeys)
   - Copy "Publishable key" (starts with `pk_test_`)
   - Copy "Secret key" (starts with `sk_test_`)

3. Update `.env` file with your keys

4. Start services:
   ```bash
   docker compose up -d
   ```

## Testing Payments

Use Stripe test card numbers:
- Success: `4242 4242 4242 4242`
- Declined: `4000 0000 0000 0002`
- Requires 3D Secure: `4000 0027 6000 3184`

**Never use real credit card numbers in test mode!**
