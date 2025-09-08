import React, { useState } from 'react';
import api from './axiosInstance';

function App() {
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('admin123');
  const [profile, setProfile] = useState(null);
  const [message, setMessage] = useState('');

  // Đăng nhập
  const login = async () => {
    try {
      const res = await api.post('/login', { username, password });
      localStorage.setItem('accessToken', res.data.token);
      localStorage.setItem('refreshToken', res.data.refreshToken);
      setMessage('Login thành công!');
    } catch (err) {
      setMessage('Đăng nhập thất bại!');
    }
  };

  // Gọi API profile (middleware sẽ tự refresh nếu cần)
  const getProfile = async () => {
    try {
      const res = await api.get('/profile');
      setProfile(res.data);
      setMessage('');
    } catch (err) {
      setMessage('Lỗi lấy profile hoặc refresh token hết hạn!');
    }
  };

  return (
    <div style={{ padding: 40 }}>
      <h2>Demo Auth + Refresh Token (middleware)</h2>
      <div>
        <input value={username} onChange={e => setUsername(e.target.value)} placeholder="Username" />
        <input value={password} onChange={e => setPassword(e.target.value)} placeholder="Password" type="password" />
        <button onClick={login}>Đăng nhập</button>
      </div>
      <button onClick={getProfile} style={{ marginTop: 20 }}>Lấy Profile (auto refresh token)</button>
      {message && <div style={{ color: 'red', marginTop: 10 }}>{message}</div>}
      {profile && (
        <pre style={{ background: '#eee', padding: 10, marginTop: 10 }}>
          {JSON.stringify(profile, null, 2)}
        </pre>
      )}
    </div>
  );
}

export default App;