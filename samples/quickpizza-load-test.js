import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// Public demo service maintained by Grafana. Override with -e BASE_URL=...
const BASE_URL = __ENV.BASE_URL || 'https://quickpizza.grafana.com';

const recommendationDuration = new Trend('pizza_recommendation_duration', true);
const recommendationSuccessRate = new Rate('pizza_recommendation_success_rate');
const pizzasRecommended = new Counter('pizzas_recommended');

export const options = {
  stages: [
    { duration: '1m', target: 50 },
    { duration: '3m', target: 50 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    pizza_recommendation_duration: ['p(95)<800'],
    pizza_recommendation_success_rate: ['rate>0.95'],
    pizzas_recommended: ['count>0'],
  },
};

export default function () {
  const params = {
    headers: {
      'Content-Type': 'application/json',
      Authorization: 'token abcdef0123456789',
    },
  };
  const body = JSON.stringify({
    maxCaloriesPerSlice: 500,
    mustBeVegetarian: false,
    excludedIngredients: [],
    excludedTools: [],
    maxNumberOfToppings: 5,
    minNumberOfToppings: 2,
  });

  const response = http.post(`${BASE_URL}/api/pizza`, body, params);

  recommendationDuration.add(response.timings.duration);
  const succeeded = check(response, { 'status is 200': (r) => r.status === 200 });
  recommendationSuccessRate.add(succeeded);
  if (succeeded) {
    pizzasRecommended.add(1);
  }

  sleep(1);
}
