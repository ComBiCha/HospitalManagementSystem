'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { LayoutDashboard, Settings, CreditCard, DollarSign, Undo2 } from 'lucide-react';

export default function AccountantLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  const menuItems = [
    { id: 'payments', href: '/accountant', label: 'Thu viện phí', icon: CreditCard },
    { id: 'deposits', href: '/accountant/deposits', label: 'Thu phí tạm ứng', icon: DollarSign },
    { id: 'refunds', href: '/accountant/refunds', label: 'Hoàn trả viện phí', icon: Undo2 },
    { id: 'reports', href: '/accountant/reports', label: 'Báo cáo', icon: LayoutDashboard },
    { id: 'settings', href: '/accountant/settings', label: 'Cài đặt', icon: Settings },
  ];

  // A simple way to map href to the main content display logic if needed
  // For now, we assume Next.js page routing handles the content display

  return (
    <div className="flex min-h-screen bg-gray-50">
      <aside className="w-64 bg-white border-r border-gray-200 p-4 space-y-2">
        <h2 className="text-xl font-bold text-gray-800 mb-4">Cổng kế toán</h2>
        <nav>
          <ul>
            {menuItems.map((item) => (
              <li key={item.id}>
                <Link
                  href={item.href}
                  className={`flex items-center px-4 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                    // Check if the current path starts with the item's href
                    // This handles active state for nested routes if any
                    pathname === item.href || (item.href !== '/accountant' && pathname.startsWith(item.href))
                      ? 'bg-blue-600 text-white shadow-md'
                      : 'text-gray-600 hover:bg-gray-100'
                  }`}>
                  <item.icon className="w-5 h-5 mr-3" />
                  {item.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </aside>

      <main className="flex-1 p-8">
        {children}
      </main>
    </div>
  );
}
