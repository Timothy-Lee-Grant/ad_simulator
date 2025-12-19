FROM node:18-alpine

WORKDIR /app

COPY FrontEnd/package.json FrontEnd/package-lock.json* ./
# Use npm install when there is no package-lock.json present (npm ci requires it)
RUN npm install --production --no-audit --no-fund

COPY FrontEnd/ .

EXPOSE 3000
CMD ["npm", "start"]
