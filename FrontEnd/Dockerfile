FROM node:18-alpine

WORKDIR /app

COPY package.json package-lock.json* ./
# Use npm install when there is no package-lock.json present (npm ci requires it)
RUN npm install --production --no-audit --no-fund

COPY . .

EXPOSE 3000
CMD ["npm", "start"]
