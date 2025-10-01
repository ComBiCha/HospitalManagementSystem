# HMS Frontend - Hospital Management System

Landing page và giao diện người dùng cho hệ thống quản lý bệnh viện được xây dựng với Next.js 14.

## Tính năng

- 🎨 Landing page hiện đại với Tailwind CSS
- 🔐 Modal đăng nhập/đăng ký với validation
- 📱 Responsive design
- 🚀 Tích hợp API authentication
- 🐳 Docker support
- ☸️ Kubernetes deployment

## Công nghệ sử dụng

- **Next.js 14** - React framework
- **TypeScript** - Type safety
- **Tailwind CSS** - Styling
- **React Hook Form** - Form handling
- **Axios** - HTTP client
- **Lucide React** - Icons

## Cài đặt

1. Cài đặt dependencies:
```bash
npm install
```

2. Copy file environment:
```bash
cp env.example .env.local
```

3. Cấu hình API URL trong `.env.local`:
```
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

4. Chạy development server:
```bash
npm run dev
```

Truy cập [http://localhost:3000](http://localhost:3000) để xem ứng dụng.

## Build và Deploy

### Docker

1. Build image:
```bash
docker build -t hms-frontend .
```

2. Chạy container:
```bash
docker run -p 3000:3000 hms-frontend
```

### Kubernetes

1. Build và push image:
```bash
docker build -t hms-frontend:latest .
docker tag hms-frontend:latest your-registry/hms-frontend:latest
docker push your-registry/hms-frontend:latest
```

2. Deploy lên Kubernetes:
```bash
kubectl apply -f ../hms-frontend-deployment.yaml
```

## Cấu trúc dự án

```
frontend/
├── app/                    # Next.js app directory
│   ├── globals.css        # Global styles
│   ├── layout.tsx         # Root layout
│   └── page.tsx           # Home page
├── components/            # React components
│   ├── AuthModal.tsx     # Login/Register modal
│   ├── Header.tsx        # Navigation header
│   ├── Hero.tsx          # Hero section
│   ├── Features.tsx      # Features section
│   └── Footer.tsx        # Footer
├── lib/                   # Utilities
│   └── api.ts            # API client
├── Dockerfile            # Docker configuration
└── package.json          # Dependencies
```

## API Integration

Frontend tích hợp với backend API thông qua các endpoints:

- `POST /api/auth/login` - Đăng nhập
- `POST /api/auth/register` - Đăng ký
- `POST /api/auth/logout` - Đăng xuất
- `POST /api/auth/refresh` - Refresh token
- `GET /api/auth/profile` - Lấy thông tin user

## Environment Variables

- `NEXT_PUBLIC_API_URL` - Backend API URL
- `NODE_ENV` - Environment (development/production)

## Scripts

- `npm run dev` - Development server
- `npm run build` - Build for production
- `npm run start` - Start production server
- `npm run lint` - Run ESLint

