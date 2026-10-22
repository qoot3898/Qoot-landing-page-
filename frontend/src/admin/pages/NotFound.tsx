import { Link } from 'react-router-dom';
import { useI18n } from '../i18n';

export function NotFoundPage() {
  const { t } = useI18n();
  return (
    <div className="page">
      <header className="page-head">
        <h1>{t.common.notFound}</h1>
        <p className="lead">{t.common.notFoundText}</p>
      </header>
      <Link className="btn" to="/">{t.nav.overview}</Link>
    </div>
  );
}
