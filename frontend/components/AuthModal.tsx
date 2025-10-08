'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { X, Eye, EyeOff, Mail, Lock, User, Phone } from 'lucide-react'
import toast from 'react-hot-toast'
import { useRouter } from 'next/navigation'
import { authApi, LoginRequest, RegisterRequest } from '../lib/api'

interface AuthModalProps {
  isOpen: boolean
  onClose: () => void
  defaultMode?: 'login' | 'register'
}

export default function AuthModal({ isOpen, onClose, defaultMode = 'login' }: AuthModalProps) {
  const [mode, setMode] = useState<'login' | 'register' | 'forgot'>('login')
  const [showPassword, setShowPassword] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const router = useRouter()

  const {
    register,
    handleSubmit,
    formState: { errors },
    reset,
    watch,
  } = useForm<LoginRequest & RegisterRequest & { confirmPassword: string }>()

  const password = watch('password')

  const onSubmit = async (data: LoginRequest | RegisterRequest) => {
    setIsLoading(true)
    try {
      let response
      if (mode === 'login') {
        response = await authApi.login(data as LoginRequest)
      } else {
        // Set role to Patient by default for web registration
        const registerData = { ...data as RegisterRequest, role: 'Patient' }
        response = await authApi.register(registerData)
      }

      const { token, refreshToken, user } = response.data
      localStorage.setItem('token', token)
      localStorage.setItem('refreshToken', refreshToken)
      localStorage.setItem('user', JSON.stringify(user))

      toast.success(mode === 'login' ? 'Đăng nhập thành công!' : 'Đăng ký thành công!')

      // Redirect based on role
      const userRole = user.role
      if (userRole === 'Patient') {
        router.push('/patient/portal')
      } else if (userRole === 'Doctor') {
        router.push('/doctor/portal')
      } else if (userRole === 'Admin') {
        router.push('/admin/dashboard')
      } else {
        router.push('/profile')
      }

      onClose()
      reset()
      window.location.reload()
    } catch (error: any) {
      console.error('Auth error:', error)
      let message = 'Có lỗi xảy ra'
      
      if (error.response?.data) {
        if (typeof error.response.data === 'string') {
          message = error.response.data
        } else if (error.response.data.message) {
          message = error.response.data.message
        } else if (error.response.data.title) {
          message = error.response.data.title
        }
      } else if (error.message) {
        message = error.message
      }
      
      toast.error(message)
    } finally {
      setIsLoading(false)
    }
  }

  const handleForgotPassword = () => {
    toast('Tính năng quên mật khẩu sẽ được phát triển sớm!', {
      icon: 'ℹ️',
    })
  }

  const switchMode = (newMode: 'login' | 'register' | 'forgot') => {
    setMode(newMode)
    reset()
  }

  if (!isOpen) return null

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="p-6">
          <div className="flex justify-between items-center mb-6">
            <h2 className="text-2xl font-bold text-gray-900">
              {mode === 'login' && 'Đăng nhập'}
              {mode === 'register' && 'Đăng ký'}
              {mode === 'forgot' && 'Quên mật khẩu'}
            </h2>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 transition-colors"
            >
              <X size={24} />
            </button>
          </div>

          {mode === 'forgot' ? (
            <div className="text-center py-8">
              <div className="w-16 h-16 bg-primary-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <Mail className="w-8 h-8 text-primary-600" />
              </div>
              <h3 className="text-lg font-semibold text-gray-900 mb-2">
                Quên mật khẩu?
              </h3>
              <p className="text-gray-600 mb-6">
                Nhập email của bạn để nhận liên kết đặt lại mật khẩu
              </p>
              <form onSubmit={handleSubmit(handleForgotPassword)}>
                <div className="mb-4">
                  <input
                    type="email"
                    placeholder="Email của bạn"
                    className="input-field"
                    {...register('email', {
                      required: 'Email là bắt buộc',
                      pattern: {
                        value: /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i,
                        message: 'Email không hợp lệ',
                      },
                    })}
                  />
                  {errors.email && (
                    <p className="text-red-500 text-sm mt-1">{errors.email.message}</p>
                  )}
                </div>
                <button
                  type="submit"
                  className="btn-primary w-full"
                  disabled={isLoading}
                >
                  Gửi liên kết đặt lại
                </button>
              </form>
            </div>
          ) : (
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              {mode === 'register' && (
                <>
                  <div className="flex gap-2">
                    <div className="w-1/2">
                      <label className="block text-sm font-medium text-gray-700 mb-1">
                        Họ
                      </label>
                      <input
                        type="text"
                        placeholder="Nhập họ"
                        className="input-field"
                        {...register('firstName', {
                          required: 'Họ là bắt buộc',
                          minLength: {
                            value: 1,
                            message: 'Họ phải có ít nhất 1 ký tự',
                          },
                        })}
                      />
                      {errors.firstName && (
                        <p className="text-red-500 text-sm mt-1">{errors.firstName.message}</p>
                      )}
                    </div>
                    <div className="w-1/2">
                      <label className="block text-sm font-medium text-gray-700 mb-1">
                        Tên
                      </label>
                      <input
                        type="text"
                        placeholder="Nhập tên"
                        className="input-field"
                        {...register('lastName', {
                          required: 'Tên là bắt buộc',
                          minLength: {
                            value: 1,
                            message: 'Tên phải có ít nhất 1 ký tự',
                          },
                        })}
                      />
                      {errors.lastName && (
                        <p className="text-red-500 text-sm mt-1">{errors.lastName.message}</p>
                      )}
                    </div>
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Tên đăng nhập
                    </label>
                    <div className="relative">
                      <User className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
                      <input
                        type="text"
                        placeholder="Nhập tên đăng nhập"
                        className="input-field pl-10"
                        {...register('username', {
                          required: 'Tên đăng nhập là bắt buộc',
                          minLength: {
                            value: 3,
                            message: 'Tên đăng nhập phải có ít nhất 3 ký tự',
                          },
                        })}
                      />
                    </div>
                    {errors.username && (
                      <p className="text-red-500 text-sm mt-1">{errors.username.message}</p>
                    )}
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Email
                    </label>
                    <div className="relative">
                      <Mail className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
                      <input
                        type="email"
                        placeholder="Nhập email"
                        className="input-field pl-10"
                        {...register('email', {
                          required: 'Email là bắt buộc',
                          pattern: {
                            value: /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i,
                            message: 'Email không hợp lệ',
                          },
                        })}
                      />
                    </div>
                    {errors.email && (
                      <p className="text-red-500 text-sm mt-1">{errors.email.message}</p>
                    )}
                  </div>

                </>
              )}

              {mode === 'login' && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Tên đăng nhập
                  </label>
                  <div className="relative">
                    <User className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
                    <input
                      type="text"
                      placeholder="Nhập tên đăng nhập"
                      className="input-field pl-10"
                      {...register('username', {
                        required: 'Tên đăng nhập là bắt buộc',
                      })}
                    />
                  </div>
                  {errors.username && (
                    <p className="text-red-500 text-sm mt-1">{errors.username.message}</p>
                  )}
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Mật khẩu
                </label>
                <div className="relative">
                  <Lock className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
                  <input
                    type={showPassword ? 'text' : 'password'}
                    placeholder="Nhập mật khẩu"
                    className="input-field pl-10 pr-10"
                    {...register('password', {
                      required: 'Mật khẩu là bắt buộc',
                      minLength: {
                        value: 6,
                        message: 'Mật khẩu phải có ít nhất 6 ký tự',
                      },
                    })}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-gray-600"
                  >
                    {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                  </button>
                </div>
                {errors.password && (
                  <p className="text-red-500 text-sm mt-1">{errors.password.message}</p>
                )}
              </div>

              {mode === 'register' && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Xác nhận mật khẩu
                  </label>
                  <div className="relative">
                    <Lock className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
                    <input
                      type={showPassword ? 'text' : 'password'}
                      placeholder="Nhập lại mật khẩu"
                      className="input-field pl-10"
                      {...register('confirmPassword', {
                        required: 'Xác nhận mật khẩu là bắt buộc',
                        validate: (value) =>
                          value === password || 'Mật khẩu xác nhận không khớp',
                      })}
                    />
                  </div>
                  {errors.confirmPassword && (
                    <p className="text-red-500 text-sm mt-1">{errors.confirmPassword.message}</p>
                  )}
                </div>
              )}

              <button
                type="submit"
                className="btn-primary w-full"
                disabled={isLoading}
              >
                {isLoading ? 'Đang xử lý...' : mode === 'login' ? 'Đăng nhập' : 'Đăng ký'}
              </button>
            </form>
          )}

          <div className="mt-6 text-center">
            {mode === 'login' && (
              <>
                <button
                  onClick={() => switchMode('forgot')}
                  className="text-primary-600 hover:text-primary-700 text-sm font-medium"
                >
                  Quên mật khẩu?
                </button>
                <p className="text-gray-600 text-sm mt-2">
                  Chưa có tài khoản?{' '}
                  <button
                    onClick={() => switchMode('register')}
                    className="text-primary-600 hover:text-primary-700 font-medium"
                  >
                    Đăng ký ngay
                  </button>
                </p>
              </>
            )}

            {mode === 'register' && (
              <p className="text-gray-600 text-sm">
                Đã có tài khoản?{' '}
                <button
                  onClick={() => switchMode('login')}
                  className="text-primary-600 hover:text-primary-700 font-medium"
                >
                  Đăng nhập
                </button>
              </p>
            )}

            {mode === 'forgot' && (
              <button
                onClick={() => switchMode('login')}
                className="text-primary-600 hover:text-primary-700 text-sm font-medium"
              >
                Quay lại đăng nhập
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
