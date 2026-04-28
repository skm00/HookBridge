import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import Header from './Header';
import Sidebar from './Sidebar';

const Layout = (): JSX.Element => {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  return (
    <div className="min-h-screen bg-background text-text">
      <div className="mx-auto flex min-h-screen w-full max-w-[1800px]">
        <Sidebar isOpen={isSidebarOpen} onClose={() => setIsSidebarOpen(false)} />

        <div className="flex min-w-0 flex-1 flex-col">
          <Header onOpenMenu={() => setIsSidebarOpen(true)} />
          <main className="flex-1 px-4 py-5 sm:px-6 lg:px-8 lg:py-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  );
};

export default Layout;
