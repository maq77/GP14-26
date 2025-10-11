import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Home, AlertCircle, MapPin, Users, Settings } from 'lucide-react';
import { clsx } from 'clsx';

const navItems = [
  { path: '/dashboard', icon: Home, label: 'Dashboard' },
  { path: '/incidents', icon: AlertCircle, label: 'Incidents' },
  { path: '/map', icon: MapPin, label: 'Map' },
  { path: '/users', icon: Users, label: 'Users' },
  { path: '/settings', icon: Settings, label: 'Settings' },
];

export const Sidebar: React.FC = () => {
  const location = useLocation();

  return (
    <aside className="w-64 bg-white shadow-md h-screen">
      <nav className="p-4">
        <ul className="space-y-2">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname.startsWith(item.path);

            return (
              <li key={item.path}>
                <Link
                  to={item.path}
                  className={clsx(
                    'flex items-center gap-3 px-4 py-3 rounded-lg transition-colors',
                    isActive
                      ? 'bg-primary-100 text-primary-700 font-semibold'
                      : 'text-gray-700 hover:bg-gray-100'
                  )}
                >
                  <Icon size={20} />
                  <span>{item.label}</span>
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>
    </aside>
  );
};
