import axios from 'axios';

const API_URL = 'https://localhost:5501/api/auth';

const api = axios.create({
  baseURL: API_URL,
});

// Thêm access token vào mỗi request
api.interceptors.request.use(config => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Tự động refresh token khi gặp lỗi 401
api.interceptors.response.use(
  response => response,
  async error => {
    const originalRequest = error.config;
    if (
      error.response &&
      error.response.status === 401 &&
      !originalRequest._retry
    ) {
      originalRequest._retry = true;
      const refreshToken = localStorage.getItem('refreshToken');
      const res = await api.post('/refresh', { refreshToken });
      if (res.status === 200) {
        localStorage.setItem('accessToken', res.data.token);
        localStorage.setItem('refreshToken', res.data.refreshToken);
        originalRequest.headers.Authorization = `Bearer ${res.data.token}`;
        return api(originalRequest);
      }
    }
    return Promise.reject(error);
  }
);

export default api;