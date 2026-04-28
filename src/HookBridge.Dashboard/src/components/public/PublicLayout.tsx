import { Outlet } from 'react-router-dom';
import PublicFooter from './PublicFooter';
import PublicNavbar from './PublicNavbar';

const PublicLayout = (): JSX.Element => {
  return (
    <div className="min-h-screen bg-background text-text">
      <PublicNavbar />
      <main>
        <Outlet />
      </main>
      <PublicFooter />
    </div>
  );
};

export default PublicLayout;
