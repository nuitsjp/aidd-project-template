import type { ReactNode } from 'react';
import { MantineProvider } from '@mantine/core';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  type AnyRoute,
  type RouteComponent,
  Outlet,
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { render } from '@testing-library/react';
import { DraftProvider } from '../../frontend/src/shared/DraftContext.tsx';

// 本番と同じ再試行なしの設定で、テストごとに独立したキャッシュを作る。
export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false, refetchOnReconnect: false },
      mutations: { retry: false },
    },
  });
}

export function TestProviders({
  queryClient,
  children,
}: {
  queryClient: QueryClient;
  children: ReactNode;
}) {
  return (
    <MantineProvider>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </MantineProvider>
  );
}

// 親のパスに画面を置くルートを作る。子ルートは返り値のaddChildrenで加える。
export function pageRoute(parent: () => AnyRoute, path: string, component: RouteComponent) {
  return createRoute({ getParentRoute: parent, path, component });
}

// 画面を実物のRouterと下書きの離脱確認の中で描画する。buildRoutesはルートの子を作る。
export function renderWithRouter(
  buildRoutes: (root: AnyRoute) => AnyRoute[],
  initialPath: string,
  queryClient = createTestQueryClient(),
) {
  const rootRoute = createRootRoute({
    component: () => (
      <DraftProvider>
        <Outlet />
      </DraftProvider>
    ),
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren(buildRoutes(rootRoute)),
    history: createMemoryHistory({ initialEntries: [initialPath] }),
  });
  const view = render(
    <TestProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </TestProviders>,
  );
  return { ...view, router, queryClient };
}
