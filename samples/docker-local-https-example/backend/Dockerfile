FROM node:20-alpine

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm install --omit=dev

# Copy application
COPY server.js .

# Expose port
EXPOSE 45000

# Run the application
CMD ["node", "server.js"]
