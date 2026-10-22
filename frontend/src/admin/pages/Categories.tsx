import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../../shared/api';
import { keys, useApiMutation, useCategories } from '../data';
import { useI18n } from '../i18n';
import type { Category } from '../types';
import { BiText, Empty, ErrorBlock, errorText, Field, PageHead, SkeletonRows, TextInput } from '../ui/controls';
import { Dialog, useConfirm, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';

interface Form { id: number | null; nameAr: string; nameEn: string; slug: string; sortOrder: number }

/** Optional news categories, in both languages; visitors can filter the news page by them. */
export function CategoriesPage() {
  const { t, number } = useI18n();
  const categories = useCategories();
  const toast = useToast();
  const confirm = useConfirm();
  const [form, setForm] = useState<Form | null>(null);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  const save = useApiMutation(
    (f: Form) =>
      api<Category>(f.id ? `/api/admin/news-categories/${f.id}` : '/api/admin/news-categories', {
        method: f.id ? 'PUT' : 'POST',
        body: { nameAr: f.nameAr, nameEn: f.nameEn, slug: f.slug, sortOrder: f.sortOrder },
      }),
    () => [keys.categories, keys.newsAll],
  );
  const remove = useApiMutation((id: number) => api(`/api/admin/news-categories/${id}`, { method: 'DELETE' }), () => [keys.categories, keys.newsAll]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!form) return;
    save.mutate(form, {
      onSuccess: () => {
        toast.show(t.news.categorySaved);
        setForm(null);
        setErrors({});
      },
      onError: (err) => {
        if (err instanceof ApiError) setErrors(err.errors);
        toast.show(errorText(err, t), 'error');
      },
    });
  };

  const onDelete = async (c: Category) => {
    if (!(await confirm({ title: t.news.categoryDeleteConfirm(c.nameAr || c.nameEn), confirm: t.common.delete, danger: true }))) return;
    remove.mutate(c.id, {
      onSuccess: () => toast.show(t.news.categoryDeleted),
      onError: (err) => toast.show(errorText(err, t), 'error'),
    });
  };

  const open = (c?: Category) => {
    setErrors({});
    setForm(c ? { id: c.id, nameAr: c.nameAr, nameEn: c.nameEn, slug: c.slug, sortOrder: c.sortOrder } : { id: null, nameAr: '', nameEn: '', slug: '', sortOrder: (categories.data?.length ?? 0) * 10 });
  };

  return (
    <div className="page">
      <PageHead
        back={<Link to="/news" className="crumb"><Icon name="chevron" className="flip" /> {t.news.title}</Link>}
        title={t.news.categories}
        actions={<button type="button" className="btn btn-primary" onClick={() => open()}>{t.news.addCategory}</button>}
      />
      {categories.isPending ? (
        <SkeletonRows rows={4} />
      ) : categories.error ? (
        <ErrorBlock error={categories.error} onRetry={() => void categories.refetch()} />
      ) : categories.data!.length === 0 ? (
        <Empty>{t.news.noCategories}</Empty>
      ) : (
        <table className="table table-narrow">
          <thead>
            <tr>
              <th scope="col" className="col-id">{t.news.categoryNameAr}</th>
              <th scope="col">{t.news.categoryNameEn}</th>
              <th scope="col">{t.news.categorySlug}</th>
              <th scope="col" className="num">{t.news.articles}</th>
              <th scope="col"><span className="sr-only">{t.common.more}</span></th>
            </tr>
          </thead>
          <tbody>
            {categories.data!.map((c) => (
              <tr key={c.id}>
                <td className="col-id" lang="ar" dir="rtl">{c.nameAr}</td>
                <td lang="en" dir="ltr">{c.nameEn || <span className="muted">{t.common.dash}</span>}</td>
                <td><code dir="ltr">{c.slug}</code></td>
                <td className="num">{number(c.articleCount)}</td>
                <td className="row-actions">
                  <button type="button" className="btn btn-sm" onClick={() => open(c)}>{t.common.edit}</button>
                  <button type="button" className="icon-btn danger" onClick={() => void onDelete(c)} aria-label={`${t.common.delete}: ${c.nameAr || c.nameEn}`}><Icon name="trash" /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <Dialog
        open={form !== null}
        onClose={() => setForm(null)}
        title={form?.id ? t.common.edit : t.news.addCategory}
        footer={
          <>
            <button type="button" className="btn" onClick={() => setForm(null)}>{t.common.cancel}</button>
            <button type="submit" form="category-form" className="btn btn-primary" disabled={save.isPending}>{save.isPending ? t.common.saving : t.common.save}</button>
          </>
        }
      >
        {form && (
          <form id="category-form" onSubmit={submit} noValidate>
            <BiText
              label={t.news.category}
              required
              maxLength={80}
              value={{ ar: form.nameAr, en: form.nameEn }}
              onChange={(v) => setForm({ ...form, nameAr: v.ar, nameEn: v.en })}
              errors={{ ar: errors.nameAr?.[0], en: errors.nameEn?.[0] }}
            />
            <Field label={t.news.categorySlug} help={t.news.slugHelp} htmlFor="cat-slug" error={errors.slug?.[0]}>
              <TextInput id="cat-slug" dir="ltr" value={form.slug} onChange={(e) => setForm({ ...form, slug: e.target.value.toLowerCase() })} />
            </Field>
            <Field label={t.news.categoryOrder} htmlFor="cat-order">
              <TextInput id="cat-order" type="number" className="input-num num" value={form.sortOrder} onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) || 0 })} />
            </Field>
          </form>
        )}
      </Dialog>
    </div>
  );
}
