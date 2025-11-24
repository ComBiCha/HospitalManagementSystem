import { useState, useEffect, useCallback } from 'react';
import { UserInfo } from '@/lib/types'; // Assuming UserInfo is defined in types.ts

interface AuthContextType {
  user: UserInfo | null;
  token: string | null;
  refreshToken: string | null;
  isLoggedIn: boolean;
  isLoading: boolean;
  login: (token: string, refreshToken: string, user: UserInfo) => void;
  logout: () => void;
}

// A simple context or just a hook for now.
// For a full application, this would typically be backed by a React Context Provider.
// For this task, a standalone hook that reads from localStorage is sufficient.

export const useAuth = (): AuthContextType => {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const loadAuthData = useCallback(() => {
    try {
      const storedToken = localStorage.getItem('token');
      const storedRefreshToken = localStorage.getItem('refreshToken');
      const storedUser = localStorage.getItem('user'); // Assuming user info is stored as a string

      if (storedToken && storedRefreshToken && storedUser) {
        setToken(storedToken);
        setRefreshToken(storedRefreshToken);
        setUser(JSON.parse(storedUser));
      }
    } catch (error) {
      console.error('Failed to load auth data from localStorage', error);
      logout(); // Clear any corrupted data
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadAuthData();
  }, [loadAuthData]);

  const login = useCallback((newToken: string, newRefreshToken: string, newUser: UserInfo) => {
    localStorage.setItem('token', newToken);
    localStorage.setItem('refreshToken', newRefreshToken);
    localStorage.setItem('user', JSON.stringify(newUser));
    setToken(newToken);
    setRefreshToken(newRefreshToken);
    setUser(newUser);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    setToken(null);
    setRefreshToken(null);
    setUser(null);
    // Optionally redirect to login page
    // window.location.href = '/';
  }, []);

  return {
    user,
    token,
    refreshToken,
    isLoggedIn: !!user,
    isLoading,
    login,
    logout,
  };
};
