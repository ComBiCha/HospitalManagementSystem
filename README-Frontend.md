# HMS Frontend - Landing Page

Landing page hiện đại cho hệ thống quản lý bệnh viện được xây dựng với Next.js 14.

## 🚀 Quick Start

### 1. Cài đặt và chạy local

```bash
cd frontend
npm install
npm run dev
```

Truy cập: http://localhost:3000

### 2. Deploy lên Kubernetes

```bash
# Chạy script deploy đơn giản
./deploy-frontend-simple.sh
```

Hoặc deploy thủ công:

```bash
cd frontend
npm install
npm run build
docker build -t hms-frontend:latest .
cd ..
kubectl apply -f hms-frontend-deployment.yaml
```

## 🎨 Tính năng

- ✅ Landing page responsive với Tailwind CSS
- ✅ Modal đăng nhập/đăng ký với validation
- ✅ Tích hợp API authentication
- ✅ Design hiện đại và user-friendly
- ✅ Docker support
- ✅ Kubernetes deployment

## 📁 Cấu trúc

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
│   ├── Features.tsx     # Features section
│   └── Footer.tsx        # Footer
├── lib/                   # Utilities
│   └── api.ts            # API client
├── Dockerfile            # Docker configuration
└── package.json          # Dependencies
```

## 🔧 API Integration

Frontend tích hợp với backend API:

- `POST /api/auth/login` - Đăng nhập
- `POST /api/auth/register` - Đăng ký  
- `POST /api/auth/logout` - Đăng xuất
- `POST /api/auth/refresh` - Refresh token
- `GET /api/auth/profile` - Lấy thông tin user

## 🌐 Access

Sau khi deploy, truy cập frontend tại:
- Local: http://localhost:3000
- Kubernetes: http://hms-frontend.local

## 📊 Monitoring

```bash
# Check pods
kubectl get pods -l app=hms-frontend

# View logs
kubectl logs -f deployment/hms-frontend

# Check service
kubectl get svc hms-frontend-service
```


